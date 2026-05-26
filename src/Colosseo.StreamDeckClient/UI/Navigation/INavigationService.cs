using Colosseo.StreamDeckClient.UI.Pages;

namespace Colosseo.StreamDeckClient.UI.Navigation;

public interface INavigationService
{
  ScrollableListPage Current { get; }
  bool CanGoBack { get; }

  void SetRoot(ScrollableListPage page);
  void Push(ScrollableListPage page);
  void Pop();
}