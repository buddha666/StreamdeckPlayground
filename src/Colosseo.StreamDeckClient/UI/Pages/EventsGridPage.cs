using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
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
/// Events grid page: events are placed at their (PositionX, PositionY) coordinates
/// instead of being listed sequentially. The grid is 8×4 (cols×rows).
/// </summary>
public sealed class EventsGridPage : ScrollableListPage, IRefreshablePage
{
    // ---- construction ----

    private readonly IStreamDeckDataManager _data;
    private readonly INavigationService _nav;
    private readonly int _bankId;
    private readonly int _tabId;
    private readonly string _tabName;
    private readonly StreamDeckClientConfig _cfg;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, CacheEntry> _thumbCache = new();
    private sealed record CacheEntry(Lazy<Task<byte[]>> LazyTask, DateTimeOffset CreatedAt, bool IsNegative);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromMinutes(10);

    private int? _lastEventsHash;
    private int? _lastQuickHash;

    // Flat array of slots, indexed by keyIndex (0..31).
    // Null means an empty slot (nothing rendered there except BACK for index 24).
    private readonly ListItem[] _slots = new ListItem[Columns * Rows];

    public EventsGridPage(
        IStreamDeckDataManager data,
        INavigationService nav,
        int bankId,
        int tabId,
        string tabName,
        StreamDeckClientConfig cfg)
    {
        _data = data;
        _nav = nav;
        _bankId = bankId;
        _tabId = tabId;
        _tabName = tabName ?? "";
        _cfg = cfg;
    }

    // ---- layout constants ----

    // BACK button lives at bottom-left: x=0, y=3 → keyIndex = 3*8+0 = 24
    public override int KeyBack => 24;

    private int StopCol => _cfg.StreamDeckShowStopButtons ? (Columns - 1) : -1;          // 7 or -1
    private int QuickCol => _cfg.StreamDeckShowQuickTab                                   // 6, 7, or -1
        ? (_cfg.StreamDeckShowStopButtons ? Columns - 2 : Columns - 1)
        : -1;
    private int MaxEventCol => Columns - 1                                                // 5, 6, or 7
        - (_cfg.StreamDeckShowStopButtons ? 1 : 0)
        - (_cfg.StreamDeckShowQuickTab ? 1 : 0);

    // ---- IRefreshablePage ----

    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        var eventsTask = _data.GetTabEventsAsync(_tabId, ct);
        var quickTask = _cfg.StreamDeckShowQuickTab
            ? _data.GetQuickTabEventsAsync(_bankId, ct)
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

        if (_lastEventsHash.HasValue && _lastEventsHash.Value == eventsHash &&
            _lastQuickHash.HasValue && _lastQuickHash.Value == quickHash)
            return false;

        _lastEventsHash = eventsHash;
        _lastQuickHash = quickHash;

        // Build slot array
        var slots = new ListItem[Columns * Rows];

        // --- stop-action buttons ---
        if (_cfg.StreamDeckShowStopButtons && StopCol >= 0)
        {
            slots[0 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdTop, "HIDE OSD TOP");
            slots[1 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdMiddle, "HIDE OSD MIDDLE");
            slots[2 * Columns + StopCol] = MakeStopItem(ListItemKind.StopOsdBottom, "HIDE OSD BOTTOM");
            slots[3 * Columns + StopCol] = MakeStopItem(ListItemKind.StopAllActions, "STOP ACTIONS");
        }

        // --- quick-tab events (first 4, placed by their PositionY) ---
        if (_cfg.StreamDeckShowQuickTab && QuickCol >= 0)
        {
            foreach (var qe in quickEvents)
            {
                var item = BuildEventItem(qe, loadingThumb: true, isQuickTab: true);
                // Here the PositionX/Y are already correct
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

            if (x < 0 || x > MaxEventCol) continue;   // outside allowed event area
            if (y < 0 || y >= Rows) continue;

            int ki = y * Columns + x;
            if (ki == KeyBack) continue;               // reserved for BACK
            if (slots[ki] != null) continue;           // occupied by stop/quick column

            slots[ki] = item;
        }

        // Publish initial state (all items show "loading" for thumbnails)
        lock (_slots)
        {
            Array.Copy(slots, _slots, slots.Length);
        }
        InvalidateAll();

        // Background thumbnail loading
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
                    var thumbId = GetThumbEventId(capturedItem);
                    byte[] thumbBytes = capturedItem.Kind == ListItemKind.EventComposite
                        && capturedItem.ChildEventIds is { Count: > 0 }
                        ? await GetCompositeThumbnailCachedAsync(thumbId, capturedItem.ChildEventIds, ct).ConfigureAwait(false)
                        : await GetThumbnailCachedAsync(thumbId, ct).ConfigureAwait(false);
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
                finally
                {
                    gate.Release();
                }
            }, ct));
            }

            await Task.WhenAll(tasks);
        }, ct);

        return true;
    }

    // ---- ScrollableListPage overrides ----

    public override bool TryGetItemByKeyIndex(int keyIndex, out ListItem item)
    {
        lock (_slots)
        {
            item = _slots[keyIndex];
        }
        // Return true for every key index — this page handles ALL slots itself.
        return true;
    }

    public override async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
    {
        if (keyIndex == KeyBack)
        {
            _nav.Pop();
            return;
        }

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

    // Required by abstract base but never called for this page type
    protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct) => Task.CompletedTask;
    protected override Task OnBackAsync(CancellationToken ct) { _nav.Pop(); return Task.CompletedTask; }

    // ---- helpers ----

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
            // Switch X and Y
            positionX: e.PositionY,
            positionY: e.PositionX,
            childEventIds: childIds
        );
    }

    private static int GetThumbEventId(ListItem item)
    {
        // Id is "S:123" or "C:123"
        return int.Parse(item.Id.Substring(2));
    }

    private static int ParseEventId(string id) => int.Parse(id.Substring(2));

    // ---- thumbnail cache ----

    private Task<byte[]> GetThumbnailCachedAsync(int thumbEventId, CancellationToken ct)
    {
        if (_thumbCache.TryGetValue(thumbEventId, out var existing)
            && existing.IsNegative
            && (DateTimeOffset.UtcNow - existing.CreatedAt) > NegativeTtl)
        {
            _thumbCache.TryRemove(thumbEventId, out _);
        }

        var entry = _thumbCache.GetOrAdd(thumbEventId, id =>
        {
            var lazy = new Lazy<Task<byte[]>>(() => FetchThumbnailBytesAsync(id), isThreadSafe: true);
            return new CacheEntry(lazy, DateTimeOffset.UtcNow, IsNegative: false);
        });

        return AwaitAndMarkNegativeAsync(thumbEventId, entry, ct);
    }

    private Task<byte[]> GetCompositeThumbnailCachedAsync(int compositeId, IReadOnlyList<int> childIds, CancellationToken ct)
    {
        if (_thumbCache.TryGetValue(compositeId, out var existing)
            && existing.IsNegative
            && (DateTimeOffset.UtcNow - existing.CreatedAt) > NegativeTtl)
        {
            _thumbCache.TryRemove(compositeId, out _);
        }

        var entry = _thumbCache.GetOrAdd(compositeId, _ =>
        {
            var lazy = new Lazy<Task<byte[]>>(
                () => FetchCompositeThumbnailBytesAsync(childIds, CancellationToken.None),
                isThreadSafe: true);
            return new CacheEntry(lazy, DateTimeOffset.UtcNow, IsNegative: false);
        });

        return AwaitAndMarkNegativeAsync(compositeId, entry, ct);
    }

    private async Task<byte[]> AwaitAndMarkNegativeAsync(int thumbEventId, CacheEntry entry, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await entry.LazyTask.Value.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch { bytes = null; }

        if (bytes == null)
        {
            _thumbCache.AddOrUpdate(
                thumbEventId,
                _ => new CacheEntry(new Lazy<Task<byte[]>>(() => Task.FromResult<byte[]>(null), true),
                                    DateTimeOffset.UtcNow, IsNegative: true),
                (_, old) => old.IsNegative ? old
                            : new CacheEntry(old.LazyTask, DateTimeOffset.UtcNow, IsNegative: true));
        }

        return bytes;
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

    private async Task<byte[]> FetchCompositeThumbnailBytesAsync(IReadOnlyList<int> childIds, CancellationToken ct)
    {
        try
        {
            // Fetch up to 4 child thumbnails in parallel (reuse the same cache)
            var ids = childIds.Take(4).ToList();
            var childBytes = await Task.WhenAll(ids.Select(id => GetThumbnailCachedAsync(id, ct))).ConfigureAwait(false);
            return Rendering.ThumbnailCompositor.Build(childBytes);
        }
        catch { return null; }
    }
}
