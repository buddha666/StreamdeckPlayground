using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.UI.Navigation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

public sealed class EventsPage : ScrollableListPage, IRefreshablePage
{
  private readonly IStreamDeckDataManager _data;
  private readonly INavigationService _nav;
  private readonly int _tabId;
  private readonly string _tabName;

  private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, CacheEntry> _thumbCache = new();
  private sealed record CacheEntry(Lazy<Task<byte[]>> LazyTask, DateTimeOffset CreatedAt, bool IsNegative);
  private static readonly TimeSpan NegativeTtl = TimeSpan.FromMinutes(10);

  private int? _lastHash;

  public EventsPage(IStreamDeckDataManager data, INavigationService nav, int tabId, string tabName)
  {
    _data = data;
    _nav = nav;
    _tabId = tabId;
    _tabName = tabName;

  }

  public override string InfoLabel
  {
    get { return _tabName.Length > 0 ? ("EVENTS: " + _tabName) : "EVENTS"; }
  }

  public bool IsLoadingThumbnails { get; private set; }

  public async Task<bool> RefreshAsync(CancellationToken ct)
  {
    var events = await _data.GetTabEventsAsync(_tabId, ct);

    var hash = HashCode.Combine(
        events.Count,
        events.Count > 0 ? events[0].Id : 0,
        events.Count > 0 ? events[^1].Id : 0
    );

    if (_lastHash.HasValue && _lastHash.Value == hash)
      return false;

    _lastHash = hash;

    // ---- phase 1: publish items immediately (no thumbnails yet) ----
    var items = new List<ListItem>(events.Count);

    // keep mapping so background tasks know which item index to update
    var thumbRequests = new List<(int itemIndex, int thumbEventId)>(events.Count);

    for (int i = 0; i < events.Count; i++)
    {
      ct.ThrowIfCancellationRequested();

      var e = events[i];
      var isComposite = e is TabEventComposition;
      var id = (isComposite ? "C:" : "S:") + e.Id.ToString();

      var accent = ColorUtil.FromFlowColor(e.Color, Color.Black);

      int? badge = null;
      int thumbEventId = e.Id;

      if (e is TabEventComposition comp)
      {
        badge = comp.TabEvents?.Count ?? 0;

        var first = comp.TabEvents?.FirstOrDefault();
        if (first != null)
          thumbEventId = first.Id;
      }

      // We don't know yet if thumbnail exists.
      // We *will try* to load it in background => mark as loading initially.
      items.Add(new ListItem(
          id: id,
          title: e.Name,
          accentColor: accent,
          badgeCount: badge,
          kind: isComposite ? ListItemKind.EventComposite : ListItemKind.EventSingle,
          thumbnailBytes: null,
          isThumbnailLoading: true
      ));

      thumbRequests.Add((i, thumbEventId));
    }

    IsLoadingThumbnails = true;
    SetItems(items);

    // ---- phase 2: fetch thumbnails in background, update items progressively ----
    _ = Task.Run(async () =>
    {
      // limit parallelism so you don't DDOS your endpoint
      using var gate = new SemaphoreSlim(4);
      var tasks = new List<Task>(thumbRequests.Count);

      foreach (var req in thumbRequests)
      {
        await gate.WaitAsync(ct);

        tasks.Add(Task.Run(async () =>
        {
          try
          {
            byte[] thumbBytes = await GetThumbnailCachedAsync(req.thumbEventId, ct).ConfigureAwait(false);
            var hasThumbnail = thumbBytes is { Length: > 0 };

            // replace item with updated copy
            var old = items[req.itemIndex];

            items[req.itemIndex] = new ListItem(
                id: old.Id,
                title: old.Title,
                accentColor: old.AccentColor,
                badgeCount: old.BadgeCount,
                kind: old.Kind,
                thumbnailBytes: hasThumbnail ? thumbBytes : null,
                isThumbnailLoading: false // IMPORTANT: loading finished (either we got bytes or we know there isn't any)
            );

            SetItems(items);
          }
          finally
          {
            gate.Release();
          }
        }, ct));
      }

      await Task.WhenAll(tasks);

      IsLoadingThumbnails = false;
      SetItems(items);
    }, ct);

    return true;
  }

  protected override async Task OnItemSelectedAsync(ListItem item, CancellationToken ct)
  {
    var id = item.Id ?? "";
    if (id.StartsWith("S:"))
    {
      var eventId = int.Parse(id.Substring(2));
      await _data.PlaySingleTabEventLive(eventId, ct);
      return;
    }

    if (id.StartsWith("C:"))
    {
      var eventId = int.Parse(id.Substring(2));
      await _data.PlayCompositeTabEventLive(eventId, ct);
      return;
    }

    // fallback (kdyby tam byl jen int)
    await _data.PlaySingleTabEventLive(int.Parse(id), ct);
  }

  protected override Task OnBackAsync(CancellationToken ct)
  {
    _nav.Pop();
    return Task.CompletedTask;
  }

  private Task<byte[]> GetThumbnailCachedAsync(int thumbEventId, CancellationToken ct)
  {
    // Expire negative entries
    if (_thumbCache.TryGetValue(thumbEventId, out var existing)
        && existing.IsNegative
        && (DateTimeOffset.UtcNow - existing.CreatedAt) > NegativeTtl)
    {
      _thumbCache.TryRemove(thumbEventId, out _);
    }

    var entry = _thumbCache.GetOrAdd(thumbEventId, id =>
    {
      var lazy = new Lazy<Task<byte[]?>>(() => FetchThumbnailBytesAsync(id), isThreadSafe: true);
      return new CacheEntry(lazy, DateTimeOffset.UtcNow, IsNegative: false);
    });

    return AwaitAndMarkNegativeAsync(thumbEventId, entry, ct);
  }

  private async Task<byte[]> AwaitAndMarkNegativeAsync(int thumbEventId, CacheEntry entry, CancellationToken ct)
  {
    byte[]? bytes;

    try
    {
      // Don't pass ct into the cached fetch task; only cancel the wait.
      bytes = await entry.LazyTask.Value.WaitAsync(ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      // don't poison the cache on cancellation
      throw;
    }
    catch
    {
      // treat unexpected failure as negative cacheable result
      bytes = null;
    }

    if (bytes == null)
    {
      // mark as negative so TTL expiry applies
      _thumbCache.AddOrUpdate(
          thumbEventId,
          _ => new CacheEntry(new Lazy<Task<byte[]?>>(() => Task.FromResult<byte[]?>(null), true),
                              DateTimeOffset.UtcNow,
                              IsNegative: true),
          (_, old) => old.IsNegative
              ? old // keep existing negative timestamp
              : new CacheEntry(old.LazyTask, DateTimeOffset.UtcNow, IsNegative: true)
      );
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
    catch
    {
      return null;
    }
  }
}
