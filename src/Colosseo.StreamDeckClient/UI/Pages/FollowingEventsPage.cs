using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Rendering;
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
/// All 32 keys are available for event slots, stop buttons, and the quick-tab column.
/// The page re-renders automatically whenever <see cref="ISelectedTabProvider.SelectedTabId"/>
/// or <see cref="ISelectedTabProvider.SelectedBankId"/> changes.
/// </summary>
public sealed class FollowingEventsPage : ScrollableListPage, IRefreshablePage
{
    // ---- construction ----

    private readonly IStreamDeckDataManager _data;
    private readonly ISelectedTabProvider _selectedTab;
    private readonly StreamDeckOptions _cfg;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Task<byte[]>> _thumbCache = new();

    private int? _lastEventsHash;
    private int? _lastQuickHash;

    // The tab/bank that was active during the last render; used to detect selection changes.
    private int? _trackedTabId;
    private int? _trackedBankId;

    // Flat array of slots, indexed by keyIndex (0..31). Null means empty.
    private readonly ListItem[] _slots = new ListItem[Columns * Rows];

    public FollowingEventsPage(
        IStreamDeckDataManager data,
        ISelectedTabProvider selectedTab,
        StreamDeckOptions cfg)
    {
        _data = data;
        _selectedTab = selectedTab;
        _cfg = cfg;
    }

    // ---- layout ----

    // No BACK button: position 24 (bottom-left) is available for a regular event.
    public override int KeyBack => -1;

    // No UP/DOWN/INFO navigation column — all 8 columns are content.
    public override bool ShowNavigationControls => false;

    private int StopCol => _cfg.ShowStopButtons ? (Columns - 1) : -1;         // 7 or -1
    private int QuickCol => _cfg.ShowQuickTab                                  // 6, 7, or -1
        ? (_cfg.ShowStopButtons ? Columns - 2 : Columns - 1)
        : -1;
    private int MaxEventCol => Columns - 1                                               // 5, 6, or 7
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

        // Hash check: if neither list changed, leave _slots as-is so loaded thumbnails persist.
        var eventsHash = HashCode.Combine(
            events.Count,
            events.Count > 0 ? events[0].Id : 0,
            events.Count > 0 ? events[^1].Id : 0
        );
        var quickHash = HashCode.Combine(
            quickEvents.Count,
            quickEvents.Count > 0 ? quickEvents[0].Id : 0,
            quickEvents.Count > 0 ? quickEvents[^1].Id : 0
        );

        bool hashChanged = !(
            _lastEventsHash.HasValue && _lastEventsHash.Value == eventsHash &&
            _lastQuickHash.HasValue && _lastQuickHash.Value == quickHash);

        if (!hashChanged)
        {
            // Data unchanged. Re-trigger background loading only if some event slots are
            // still in a loading state (e.g., the previous background pass was interrupted).
            bool anyStillLoading;
            lock (_slots)
            {
                anyStillLoading = Array.Exists(_slots, s =>
                    s is { IsThumbnailLoading: true } &&
                    s.Kind is ListItemKind.EventSingle or ListItemKind.EventComposite);
            }
            if (!anyStillLoading)
                return false;
            // Fall through to restart the thumbnail loading pass for the pending slots.
        }
        else
        {
            _lastEventsHash = eventsHash;
            _lastQuickHash = quickHash;

            // Build slot array.
            var slots = new ListItem[Columns * Rows];

            // --- stop-action buttons ---
            if (_cfg.ShowStopButtons && StopCol >= 0)
            {
                slots[0 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdTop, "HIDE OSD TOP");
                slots[1 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdMiddle, "HIDE OSD MIDDLE");
                slots[2 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdBottom, "HIDE OSD BOTTOM");
                slots[3 * Columns + StopCol] = MakeStopItem(ListItemKind.StopAllActions, "STOP ACTIONS");
            }

            // --- quick-tab events (placed by their PositionY) ---
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
            // KeyBack == -1 so no slot is reserved for navigation — all positions are fair game.
            foreach (var e in events)
            {
                var item = BuildEventItem(e, loadingThumb: true);
                int x = item.PositionX;
                int y = item.PositionY;

                if (x < 0 || x > MaxEventCol) continue;   // outside allowed event area
                if (y < 0 || y >= Rows) continue;

                int ki = y * Columns + x;
                if (slots[ki] != null) continue;            // occupied by stop/quick column

                slots[ki] = item;
            }

            // Publish initial state (thumbnails show "loading" placeholder).
            lock (_slots)
            {
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
                        var updated = new ListItem(
                            id: capturedItem.Id,
                            title: capturedItem.Title,
                            accentColor: capturedItem.AccentColor,
                            badgeCount: capturedItem.BadgeCount,
                            kind: capturedItem.Kind,
                            thumbnailBytes: thumbBytes is { Length: > 0 } ? thumbBytes : null,
                            isThumbnailLoading: false,
                            isQuickTab: capturedItem.IsQuickTab,
                            positionX: capturedItem.PositionX,
                            positionY: capturedItem.PositionY,
                            childEventIds: capturedItem.ChildEventIds
                        );
                        lock (_slots) { _slots[capturedKi] = updated; }
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
        lock (_slots) { item = _slots[keyIndex]; }
        // Return true for every key index — this page handles ALL slots itself.
        return true;
    }

    public override async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
    {
        ListItem item;
        lock (_slots) { item = _slots[keyIndex]; }
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
        }
    }

    // Required by abstract base but never called for this page type.
    protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct) => Task.CompletedTask;
    protected override Task OnBackAsync(CancellationToken ct) => Task.CompletedTask;

    // ---- helpers ----

    private static int ParseEventId(string id) => int.Parse(id.Substring(2));

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
