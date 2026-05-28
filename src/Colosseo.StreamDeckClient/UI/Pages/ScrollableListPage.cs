using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

public abstract class ScrollableListPage
{
  public const int Columns = 8;
  public const int Rows = 4;
  public const int ReservedCol = 7;

  /// <summary>
  /// When true (default) column 7 is reserved for UP / DOWN / Info / Back navigation controls.
  /// Override to false in pages that want all 8 columns filled with content items.
  /// </summary>
  public virtual bool ShowNavigationControls => true;

  /// <summary>
  /// Number of content items per page.
  /// Standard pages: 7 columns × 4 rows = 28.
  /// Full-grid pages (ShowNavigationControls=false): 8 columns × 4 rows − 1 back key = 31.
  /// </summary>
  public int PageSize => ShowNavigationControls ? (Columns - 1) * Rows : Columns * Rows - 1;

  private readonly object _sync = new();

  public int Offset
  {
    get { lock (_sync) return _offset; }
    private set { lock (_sync) _offset = value; }
  }
  private int _offset;

  public virtual string InfoLabel
  {
    get { return ""; }
  }

  protected IReadOnlyList<ListItem> Items
  {
    get { lock (_sync) return _items; }
    private set { lock (_sync) _items = value; }
  }
  private IReadOnlyList<ListItem> _items = Array.Empty<ListItem>();

  public int ItemCount
  {
    get { lock (_sync) return _items.Count; }
  }

  public bool TryGetItem(int absoluteIndex, out ListItem item)
  {
    lock (_sync)
    {
      if (absoluteIndex >= 0 && absoluteIndex < _items.Count)
      {
        item = _items[absoluteIndex];
        return true;
      }
    }

    item = null;
    return false;
  }

  /// <summary>
  /// Grid-pages (e.g. EventsGridPage) override this to expose items directly by key index
  /// rather than through the offset-based list mapping. Returns false on the base class.
  /// </summary>
  public virtual bool TryGetItemByKeyIndex(int keyIndex, out ListItem item)
  {
    item = null;
    return false;
  }

  public bool CanScrollUp
  {
    get { lock (_sync) return _offset > 0; }
  }

  public bool CanScrollDown
  {
    get { lock (_sync) return _offset + PageSize < _items.Count; }
  }

  public void SetItems(IReadOnlyList<ListItem> items)
  {
    lock (_sync)
    {
      _items = items ?? Array.Empty<ListItem>();

      if (_offset > 0 && _offset >= _items.Count)
        _offset = (_items.Count / PageSize) * PageSize;

      MarkAllDirty_NoLock();
    }
  }

  public void ScrollUp()
  {
    lock (_sync)
    {
      if (!CanScrollUp) return;
      _offset = Math.Max(0, _offset - PageSize);
      MarkAllDirty_NoLock();
    }
  }

  public void ScrollDown()
  {
    lock (_sync)
    {
      if (!CanScrollDown) return;
      _offset = Math.Min(Math.Max(0, _items.Count - 1), _offset + PageSize);
      _offset = (_offset / PageSize) * PageSize;
      MarkAllDirty_NoLock();
    }
  }

  public void InvalidateAll()
  {
    lock (_sync)
    {
      MarkAllDirty_NoLock();
    }
  }

  public int? TryMapKeyToItemIndex(int keyIndex)
  {
    lock (_sync)
    {
      // Back button is never an item
      if (keyIndex == KeyBack) return null;

      if (!ShowNavigationControls)
      {
        // Full-grid: every key except KeyBack maps directly to a sequential item index.
        // Keys before KeyBack map 1:1; keys after KeyBack shift down by 1.
        var indexInPage = keyIndex < KeyBack ? keyIndex : keyIndex - 1;
        var itemIndex = _offset + indexInPage;
        return itemIndex < _items.Count ? itemIndex : (int?)null;
      }

      // Standard scroll-list: column 7 is reserved for navigation keys.
      var col = keyIndex % Columns;
      var row = keyIndex / Columns;

      if (row < 0 || row >= Rows) return null;
      if (col == ReservedCol) return null;

      var indexInPageStd = row * (Columns - 1) + col;
      var itemIndexStd = _offset + indexInPageStd;

      return itemIndexStd < _items.Count ? itemIndexStd : (int?)null;
    }
  }

  // ---- Navigation keys ----

  public int KeyScrollUp { get { return 7; } }
  public int KeyScrollDown { get { return 15; } }
  public int KeyInfo { get { return 23; } }

  /// <summary>
  /// Index of the BACK key. Default is 31 (bottom-right). Override to move it
  /// to a different position (e.g. 24 for bottom-left).
  /// </summary>
  public virtual int KeyBack { get { return 31; } }

  // ---- Dirty-key tracking ----

  private readonly HashSet<int> _dirtyKeys = new();

  public IReadOnlyCollection<int> DirtyKeys
  {
    get
    {
      lock (_sync)
      {
        return _dirtyKeys.ToArray();
      }
    }
  }

  /// <summary>
  /// Atomically snapshots and clears the dirty-key set.
  /// </summary>
  public int[] TakeAndClearDirtyKeys()
  {
    lock (_sync)
    {
      var keys = _dirtyKeys.ToArray();
      _dirtyKeys.Clear();
      return keys;
    }
  }

  protected void MarkAllDirty()
  {
    lock (_sync)
    {
      MarkAllDirty_NoLock();
    }
  }

  private void MarkAllDirty_NoLock()
  {
    _dirtyKeys.Clear();
    for (int i = 0; i < Columns * Rows; i++)
      _dirtyKeys.Add(i);
  }

  /// <summary>Marks a single key as dirty (thread-safe). Used by grid pages for per-slot updates.</summary>
  protected internal void MarkKeyDirty(int keyIndex)
  {
    lock (_sync)
    {
      _dirtyKeys.Add(keyIndex);
    }
  }

  public void ClearDirty()
  {
    lock (_sync)
    {
      _dirtyKeys.Clear();
    }
  }

  public virtual async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
  {
    if (ShowNavigationControls && keyIndex == KeyScrollUp) { ScrollUp(); return; }
    if (ShowNavigationControls && keyIndex == KeyScrollDown) { ScrollDown(); return; }
    if (keyIndex == KeyBack) { await OnBackAsync(ct); return; }

    int? itemIndex = TryMapKeyToItemIndex(keyIndex);
    if (!itemIndex.HasValue) return;

    ListItem item;
    lock (_sync)
    {
      if (itemIndex.Value < 0 || itemIndex.Value >= _items.Count) return;
      item = _items[itemIndex.Value];
    }

    await OnItemSelectedAsync(item, ct);
  }

  protected abstract Task OnItemSelectedAsync(ListItem item, CancellationToken ct);
  protected abstract Task OnBackAsync(CancellationToken ct);
}
