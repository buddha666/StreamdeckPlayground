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

  public int PageSize { get { return (Columns - 1) * Rows; } } // 28

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
      var col = keyIndex % Columns;
      var row = keyIndex / Columns;

      if (row < 0 || row >= Rows) return null;
      if (col == ReservedCol) return null;

      var indexInPage = row * (Columns - 1) + col;
      var itemIndex = _offset + indexInPage;

      return itemIndex < _items.Count ? itemIndex : (int?)null;
    }
  }

  public int KeyScrollUp { get { return 7; } }
  public int KeyScrollDown { get { return 15; } }
  public int KeyInfo { get { return 23; } }
  public int KeyBack { get { return 31; } }

  private readonly HashSet<int> _dirtyKeys = new();
  public IReadOnlyCollection<int> DirtyKeys
  {
    get
    {
      lock (_sync)
      {
        // return a snapshot to avoid "collection modified" in renderer
        return _dirtyKeys.ToArray();
      }
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

  public void ClearDirty()
  {
    lock (_sync)
    {
      _dirtyKeys.Clear();
    }
  }

  public async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
  {
    if (keyIndex == KeyScrollUp) { ScrollUp(); return; }
    if (keyIndex == KeyScrollDown) { ScrollDown(); return; }
    if (keyIndex == KeyBack) { await OnBackAsync(ct); return; }

    int? itemIndex = TryMapKeyToItemIndex(keyIndex);
    if (!itemIndex.HasValue) return;

    ListItem item;
    lock (_sync)
    {
      // index could be out-of-range if items changed concurrently
      if (itemIndex.Value < 0 || itemIndex.Value >= _items.Count) return;
      item = _items[itemIndex.Value];
    }

    await OnItemSelectedAsync(item, ct);
  }

  protected abstract Task OnItemSelectedAsync(ListItem item, CancellationToken ct);
  protected abstract Task OnBackAsync(CancellationToken ct);
}