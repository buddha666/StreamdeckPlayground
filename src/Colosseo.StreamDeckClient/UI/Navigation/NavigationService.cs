using Colosseo.StreamDeckClient.UI.Pages;
using System;
using System.Collections.Generic;

namespace Colosseo.StreamDeckClient.UI.Navigation;

public sealed class NavigationService : INavigationService
{
  private readonly Stack<ScrollableListPage> _stack = new();

  public ScrollableListPage Current
      => _stack.Count > 0 ? _stack.Peek() : throw new InvalidOperationException("Navigation stack is empty.");

  public bool CanGoBack => _stack.Count > 1;

  public void SetRoot(ScrollableListPage page)
  {
    _stack.Clear();
    _stack.Push(page);
  }

  public void Push(ScrollableListPage page)
  {
    if (page == null) throw new ArgumentNullException(nameof(page));

    _stack.Push(page);

    // ensure render of new page
    page.InvalidateAll();
  }

  public void Pop()
  {
    if (!CanGoBack) return;

    _stack.Pop();

    // ensure render of page we returned to
    Current.InvalidateAll();
  }
}
