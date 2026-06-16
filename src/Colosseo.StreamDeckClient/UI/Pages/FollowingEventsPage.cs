using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Device;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.UI.Navigation;
using Monogram.Sport.FlowBLL.Enums;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

/// <summary>
/// Following-mode events page: shows events for the tab that is currently selected
/// in <see cref="ISelectedTabProvider"/> without any navigation stack or BACK button.
/// All keys are available for event slots, stop buttons, and the quick-tab column.
/// The page re-renders automatically whenever <see cref="ISelectedTabProvider.SelectedTabId"/>
/// or <see cref="ISelectedTabProvider.SelectedBankId"/> changes.
/// On 3-row devices the stop column shows a HIDE OSD navigation button (above STOP ACTIONS)
/// that opens <see cref="HideOsdSubPage"/> for the individual OSD-hide buttons.
/// </summary>
public sealed class FollowingEventsPage : ScrollableListPage, IRefreshablePage
{
    // ---- construction ----

    private readonly IStreamDeckDataManager _data;
    private readonly ISelectedTabProvider _selectedTab;
    private readonly INavigationService _nav;
    private readonly StreamDeckOptions _cfg;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Task<byte[]>> _thumbCache = new();

    private int? _lastEventsHash;
    private int? _lastQuickHash;

    // The tab/bank that was active during the last render; used to detect selection changes.
    private int? _trackedTabId;
    private int? _trackedBankId;

    // Flat array of slots, indexed by keyIndex (0..TotalKeys-1). Null means empty.
    private readonly ListItem[] _slots;

    public FollowingEventsPage(
        IStreamDeckDataManager data,
        ISelectedTabProvider selectedTab,
        INavigationService nav,
        StreamDeckOptions cfg,
        DeviceLayout layout = null)
    {
        Layout = layout ?? DeviceLayout.Default;
        _slots = new ListItem[Layout.TotalKeys];

        _data = data;
        _selectedTab = selectedTab;
        _nav = nav;
        _cfg = cfg;
    }

    // ---- layout ----

    // No BACK button: following mode is a root page, the user never goes back from it.
    public override int KeyBack => -1;

    // No UP/DOWN/INFO navigation column — all columns are content.
    public override bool ShowNavigationControls => false;

    private int StopCol => _cfg.ShowStopButtons ? (Columns - 1) : -1;
    private int QuickCol => _cfg.ShowQuickTab
        ? (_cfg.ShowStopButtons ? Columns - 2 : Columns - 1)
        : -1;
    private int MaxEventCol => Columns - 1
        - (_cfg.ShowStopButtons ? 1 : 0)
        - (_cfg.ShowQuickTab ? 1 : 0);

    // ---- IRefreshablePage ----

    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        var tabId = _selectedTab.SelectedTabId;
        var bankId = _selectedTab.SelectedBankId;

        // No tab selected yet — clear the screen once and wait.
        if (tabId == null || bankId == null)
        {
            if (_trackedTabId != null || _trackedBankId != null)
            {
                _trackedTabId = null;
                _trackedBankId = null;
                _lastEventsHash = null;
                _lastQuickHash = null;
                lock (_slots) { Array.Clear(_slots, 0, _slots.Length); }
                InvalidateAll();
                return true;
            }
            return false;
        }

        // Selected tab/bank changed — reset hashes to force a full re-render.
        if (tabId != _trackedTabId || bankId != _trackedBankId)
        {
            _trackedTabId = tabId;
            _trackedBankId = bankId;
            _lastEventsHash = null;
            _lastQuickHash = null;
        }

        var eventsTask = _data.GetTabEventsAsync(tabId.Value, ct);
        var quickTask = _cfg.ShowQuickTab
            ? _data.GetQuickTabEventsAsync(bankId.Value, ct)
            : Task.FromResult<IReadOnlyList<ITabEventBase>>(Array.Empty<ITabEventBase>());

        await Task.WhenAll(eventsTask, quickTask);

        var events = await eventsTask;
        var quickEvents = await quickTask;

        static int ComputeEventsHash(System.Collections.Generic.IReadOnlyList<ITabEventBase> list)
        {
            var h = new HashCode();
            foreach (var e in list)
            {
                h.Add(e.Id);
                h.Add(e.Name);
                h.Add(e.Color);
                h.Add(e.PositionX);
                h.Add(e.PositionY);
                if (e is TabEventComposition comp)
                    h.Add(comp.TabEvents?.Count ?? 0);
            }
            return h.ToHashCode();
        }

        var eventsHash = ComputeEventsHash(events);
        var quickHash  = ComputeEventsHash(quickEvents);

        bool hashChanged = !(
            _lastEventsHash.HasValue && _lastEventsHash.Value == eventsHash &&
            _lastQuickHash.HasValue && _lastQuickHash.Value == quickHash);

        if (!hashChanged)
        {
            bool anyStillLoading;
            lock (_slots)
            {
                anyStillLoading = Array.Exists(_slots, s =>
                    s is { IsThumbnailLoading: true } &&
                    s.Kind is ListItemKind.EventSingle or ListItemKind.EventComposite);
            }
            if (!anyStillLoading)
            {
                // Defensive: even when the hash reports no change, do a targeted scan for
                // metadata (title, colour) that the hash may have missed.  This covers edge
                // cases where a composite event's Name is returned as null or a stale cached
                // value by the OData layer during incremental polls.
                var dirtyKis = new List<int>();
                lock (_slots)
                {
                    foreach (var e in events)
                    {
                        int x = e.PositionY;  // BuildEventItem swaps X/Y
                        int y = e.PositionX;
                        if (x < 0 || x > MaxEventCol || y < 0 || y >= Rows) continue;
                        int ki = y * Columns + x;
                        if (ki < 0 || ki >= _slots.Length) continue;
                        AppendIfStaleMeta(e, ki, dirtyKis);
                    }

                    if (_cfg.ShowQuickTab && QuickCol >= 0)
                    {
                        foreach (var qe in quickEvents)
                        {
                            int row = qe.PositionX;  // after X/Y swap: item.PositionY = e.PositionX
                            if (row < 0 || row >= Rows) continue;
                            int ki = row * Columns + QuickCol;
                            if (ki < 0 || ki >= _slots.Length) continue;
                            AppendIfStaleMeta(qe, ki, dirtyKis);
                        }
                    }
                }
                if (dirtyKis.Count == 0)
                    return false;
                foreach (var ki in dirtyKis)
                    MarkKeyDirty(ki);
                return true;
            }
        }
        else
        {
            _lastEventsHash = eventsHash;
            _lastQuickHash = quickHash;

            var slots = new ListItem[Layout.TotalKeys];

            // --- stop-action buttons ---
            if (_cfg.ShowStopButtons && StopCol >= 0)
            {
                if (Layout.IsThreeRow)
                {
                    // 3-row layout: bottom = STOP ACTIONS, above it = HIDE OSD navigation button.
                    // Row 0 of the stop column remains empty.
                    slots[1 * Columns + StopCol] = MakeStopItem(ListItemKind.HideOsdMenu, "HIDE OSD");
                    slots[2 * Columns + StopCol] = MakeStopItem(ListItemKind.StopAllActions, "STOP ACTIONS");
                }
                else
                {
                    slots[0 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdTop,    "HIDE OSD TOP");
                    slots[1 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdMiddle, "HIDE OSD MIDDLE");
                    slots[2 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdBottom, "HIDE OSD BOTTOM");
                    slots[3 * Columns + StopCol] = MakeStopItem(ListItemKind.StopAllActions, "STOP ACTIONS");
                }
            }

            // --- quick-tab events (placed by their PositionY, bounded by Rows) ---
            if (_cfg.ShowQuickTab && QuickCol >= 0)
            {
                foreach (var qe in quickEvents)
                {
                    var item = BuildEventItem(qe, loadingThumb: true, isQuickTab: true);
                    if (item.PositionY < 0 || item.PositionY >= Rows)
                        continue;
                    int ki = item.PositionY * Columns + QuickCol;
                    slots[ki] = item;
                }
            }

            // --- regular events ---
            foreach (var e in events)
            {
                var item = BuildEventItem(e, loadingThumb: true);
                int x = item.PositionX;
                int y = item.PositionY;

                if (x < 0 || x > MaxEventCol) continue;
                if (y < 0 || y >= Rows) continue;

                int ki = y * Columns + x;
                if (slots[ki] != null) continue;

                slots[ki] = item;
            }

            lock (_slots)
            {
                // Preserve already-loaded thumbnails for items that stayed at the same slot.
                for (int i = 0; i < slots.Length; i++)
                {
                    var fresh    = slots[i];
                    var existing = _slots[i];
                    if (fresh != null && fresh.IsThumbnailLoading
                        && existing != null && !existing.IsThumbnailLoading
                        && fresh.Id == existing.Id)
                    {
                        slots[i] = new ListItem(
                            id: fresh.Id,
                            title: fresh.Title,
                            accentColor: fresh.AccentColor,
                            badgeCount: fresh.BadgeCount,
                            kind: fresh.Kind,
                            thumbnailBytes: existing.ThumbnailBytes,
                            isThumbnailLoading: false,
                            isQuickTab: fresh.IsQuickTab,
                            positionX: fresh.PositionX,
                            positionY: fresh.PositionY,
                            childEventIds: fresh.ChildEventIds
                        );
                    }
                }
                Array.Copy(slots, _slots, slots.Length);
            }
            InvalidateAll();
        }

        // Background thumbnail loading.
        _ = Task.Run(async () =>
        {
            using var gate = new SemaphoreSlim(4);
            var tasks = new List<Task>();

            for (int ki = 0; ki < _slots.Length; ki++)
            {
                ListItem item;
                lock (_slots) { item = _slots[ki]; }

                if (item == null || !item.IsThumbnailLoading) continue;
                if (item.Kind is not (ListItemKind.EventSingle or ListItemKind.EventComposite)) continue;

                int capturedKi = ki;
                ListItem capturedItem = item;

                await gate.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        byte[] thumbBytes = capturedItem.Kind == ListItemKind.EventComposite
                            ? await GetCompositeThumbnailAsync(capturedItem.ChildEventIds ?? Array.Empty<int>()).ConfigureAwait(false)
                            : await GetThumbnailCachedAsync(ParseEventId(capturedItem.Id)).ConfigureAwait(false);

                        // Read the CURRENT slot state inside the lock so we never write back
                        // stale metadata (title / color / position) that may have been updated
                        // by a newer RefreshAsync cycle while the thumbnail was being fetched.
                        lock (_slots)
                        {
                            var current = _slots[capturedKi];
                            if (current != null && current.Id == capturedItem.Id)
                            {
                                _slots[capturedKi] = new ListItem(
                                    id: current.Id,
                                    title: current.Title,
                                    accentColor: current.AccentColor,
                                    badgeCount: current.BadgeCount,
                                    kind: current.Kind,
                                    thumbnailBytes: thumbBytes is { Length: > 0 } ? thumbBytes : null,
                                    isThumbnailLoading: false,
                                    isQuickTab: current.IsQuickTab,
                                    positionX: current.PositionX,
                                    positionY: current.PositionY,
                                    childEventIds: current.ChildEventIds
                                );
                            }
                        }
                        MarkKeyDirty(capturedKi);
                    }
                    catch
                    {
                        // Leave the slot in IsThumbnailLoading=true so the stale-loading check
                        // in the next RefreshAsync cycle can schedule another retry pass.
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);
        }, ct);

        return hashChanged;
    }

    // ---- ScrollableListPage overrides ----

    public override bool TryGetItemByKeyIndex(int keyIndex, out ListItem item)
    {
        lock (_slots) { item = (keyIndex >= 0 && keyIndex < _slots.Length) ? _slots[keyIndex] : null; }
        return true;
    }

    public override async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
    {
        ListItem item;
        lock (_slots) { item = (keyIndex >= 0 && keyIndex < _slots.Length) ? _slots[keyIndex] : null; }
        if (item == null) return;

        switch (item.Kind)
        {
            case ListItemKind.EventSingle:
                await _data.PlaySingleTabEventLive(ParseEventId(item.Id), ct);
                break;
            case ListItemKind.EventComposite:
                await _data.PlayCompositeTabEventLive(ParseEventId(item.Id), ct);
                break;
            case ListItemKind.StopOsdTop:
                await _data.HideOsd(SystemEventTypeCode.OsdTop, ct);
                break;
            case ListItemKind.StopOsdMiddle:
                await _data.HideOsd(SystemEventTypeCode.OsdMiddle, ct);
                break;
            case ListItemKind.StopOsdBottom:
                await _data.HideOsd(SystemEventTypeCode.OsdBottom, ct);
                break;
            case ListItemKind.StopAllActions:
                await _data.StopAction(ct);
                break;
            case ListItemKind.HideOsdMenu:
                // 3-row device: open the OSD sub-page.
                _nav.Push(new HideOsdSubPage(_data, _nav, _cfg, Layout));
                break;
        }
    }

    // Required by abstract base but never called for this page type.
    protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct) => Task.CompletedTask;
    protected override Task OnBackAsync(CancellationToken ct) => Task.CompletedTask;

    // ---- helpers ----

    private static int ParseEventId(string id) => int.Parse(id.Substring(2));

    /// <summary>
    /// Must be called while <see cref="_slots"/> is locked.
    /// Checks whether the slot at <paramref name="ki"/> holds the same event as
    /// <paramref name="e"/> but with a stale title or colour; if so, updates the
    /// slot in-place and records <paramref name="ki"/> in <paramref name="dirtyKis"/>.
    /// </summary>
    private void AppendIfStaleMeta(ITabEventBase e, int ki, List<int> dirtyKis)
    {
        var slot = _slots[ki];
        if (slot == null || slot.IsThumbnailLoading) return;

        var expectedId = (e is TabEventComposition ? "C:" : "S:") + e.Id;
        if (slot.Id != expectedId) return;

        var newColor = ColorUtil.FromFlowColor(e.Color, Color.Black);
        if (slot.Title == e.Name && slot.AccentColor == newColor) return;

        _slots[ki] = new ListItem(
            id: slot.Id,
            title: e.Name,
            accentColor: newColor,
            badgeCount: slot.BadgeCount,
            kind: slot.Kind,
            thumbnailBytes: slot.ThumbnailBytes,
            isThumbnailLoading: false,
            isQuickTab: slot.IsQuickTab,
            positionX: slot.PositionX,
            positionY: slot.PositionY,
            childEventIds: slot.ChildEventIds
        );
        dirtyKis.Add(ki);
    }

    private static ListItem MakeStopItem(ListItemKind kind, string title) =>
        new ListItem(
            id: kind.ToString(),
            title: title,
            accentColor: Color.DarkRed,
            badgeCount: null,
            kind: kind,
            thumbnailBytes: null,
            isThumbnailLoading: false
        );

    private static ListItem BuildEventItem(ITabEventBase e, bool loadingThumb, bool isQuickTab = false)
    {
        var isComposite = e is TabEventComposition;
        var id = (isComposite ? "C:" : "S:") + e.Id.ToString();
        var accent = ColorUtil.FromFlowColor(e.Color, Color.Black);
        int? badge = null;
        IReadOnlyList<int> childIds = null;
        if (e is TabEventComposition comp)
        {
            badge = comp.TabEvents?.Count ?? 0;
            childIds = comp.TabEvents?.Select(t => t.Id).ToList();
        }

        return new ListItem(
            id: id,
            title: e.Name,
            accentColor: accent,
            badgeCount: badge,
            kind: isComposite ? ListItemKind.EventComposite : ListItemKind.EventSingle,
            thumbnailBytes: null,
            isThumbnailLoading: loadingThumb,
            isQuickTab: isQuickTab,
            // Switch X and Y (same convention as EventsGridPage)
            positionX: e.PositionY,
            positionY: e.PositionX,
            childEventIds: childIds
        );
    }

    // ---- thumbnail cache ----

    private Task<byte[]> GetThumbnailCachedAsync(int tabEventId)
    {
        if (_thumbCache.TryGetValue(tabEventId, out var existing)
            && existing.IsCompleted
            && (existing.IsFaulted || existing.IsCanceled
                || (existing.IsCompletedSuccessfully && existing.Result == null)))
        {
            _thumbCache.TryRemove(tabEventId, out _);
        }
        return _thumbCache.GetOrAdd(tabEventId, id => FetchThumbnailBytesAsync(id));
    }

    private async Task<byte[]> GetCompositeThumbnailAsync(IReadOnlyList<int> childIds)
    {
        try
        {
            var ids = childIds.Take(4).ToList();
            var childBytes = new byte[ids.Count][];
            for (int i = 0; i < ids.Count; i++)
            {
                try { childBytes[i] = await GetThumbnailCachedAsync(ids[i]).ConfigureAwait(false); }
                catch { childBytes[i] = null; }
            }
            return Rendering.ThumbnailCompositor.Build(childBytes);
        }
        catch { return null; }
    }

    private async Task<byte[]> FetchThumbnailBytesAsync(int thumbEventId)
    {
        try
        {
            await using var s = await _data.GetTabEventThumbnailAsync(thumbEventId, CancellationToken.None).ConfigureAwait(false);
            if (s == null) return null;
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, CancellationToken.None).ConfigureAwait(false);
            var bytes = ms.ToArray();
            return bytes.Length > 0 ? bytes : null;
        }
        catch { return null; }
    }
}
