using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Colosseo.StreamDeckClient.Device;

namespace Colosseo.StreamDeckClient.UI.Pages;

public abstract class ScrollableListPage
{
  // ---- Dynamic layout ----

  /// <summary>
  /// Device layout providing dynamic rows/columns.
  /// Subclasses should assign this in their constructor (before any dirty-key operations).
  /// Defaults to the 8×4 Stream Deck XL layout.
  /// </summary>
  public DeviceLayout Layout { get; protected set; } = DeviceLayout.Default;

  public int Columns => Layout.Columns;
  public int Rows    => Layout.Rows;
  public int ReservedCol => Layout.Columns - 1;

  /// <summary>
  /// When true (default) column (Columns-1) is reserved for UP / DOWN / Info / Back navigation controls.
  /// Override to false in pages that want all columns filled with content items.
  /// </summary>
  public virtual bool ShowNavigationControls => true;

  /// <summary>
  /// Number of content items per page.
  /// Standard pages (ShowNavigationControls=true): (Columns-1) × Rows.
  /// Full-grid pages with back key (ShowNavigationControls=false, KeyBack ≥ 0): Columns × Rows − 1.
  /// Full-grid pages without back key (ShowNavigationControls=false, KeyBack &lt; 0): Columns × Rows.
  /// </summary>
  public int PageSize => ShowNavigationControls
      ? (Columns - 1) * Rows
      : (KeyBack < 0 ? Columns * Rows : Columns * Rows - 1);

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
      // Back button is never an item (skip check when KeyBack < 0 = no back key)
      if (KeyBack >= 0 && keyIndex == KeyBack) return null;

      if (!ShowNavigationControls)
      {
        // Full-grid: every key (except the optional back key) maps directly to a sequential item index.
        // When KeyBack < 0 all keys map 1:1; otherwise keys after KeyBack shift down by 1.
        var indexInPage = (KeyBack < 0 || keyIndex < KeyBack) ? keyIndex : keyIndex - 1;
        var itemIndex = _offset + indexInPage;
        return itemIndex < _items.Count ? itemIndex : (int?)null;
      }

      // Standard scroll-list: last column is reserved for navigation keys.
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

  public int KeyScrollUp => Layout.KeyScrollUp;
  public int KeyScrollDown => Layout.KeyScrollDown;
  public int KeyInfo => Layout.KeyInfo;

  /// <summary>
  /// Index of the BACK key. Defaults to bottom-right (last key). Override to move it
  /// to a different position (e.g. bottom-left). Use -1 for no back button.
  /// </summary>
  public virtual int KeyBack => Layout.KeyBackDefault;

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
    for (int i = 0; i < Layout.TotalKeys; i++)
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
    if (KeyBack >= 0 && keyIndex == KeyBack) { await OnBackAsync(ct); return; }

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
