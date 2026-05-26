using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.UI.Navigation;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

public sealed class TabsPage : ScrollableListPage, IRefreshablePage
{
  private readonly IStreamDeckDataManager _data;
  private readonly INavigationService _nav;
  private readonly int _bankId;
  private readonly string _bankName;

  private int? _lastHash;

  public TabsPage(IStreamDeckDataManager data, INavigationService nav, int bankId, string bankName)
  {
    _data = data;
    _nav = nav;
    _bankId = bankId;
    _bankName = bankName ?? "";
  }

  public override string InfoLabel
  {
    get
    {
      // krátké a čitelné
      return _bankName.Length > 0 ? ("TABS: " + _bankName) : "TABS";
    }
  }

  public async Task<bool> RefreshAsync(CancellationToken ct)
  {
    var tabs = await _data.GetTabsAsync(_bankId, ct);

    var hash = HashCode.Combine(
        tabs.Count,
        tabs.Count > 0 ? tabs[0].Version : 0,
        tabs.Count > 0 ? tabs[^1].Version : 0
    );

    if (_lastHash.HasValue && _lastHash.Value == hash)
      return false;

    _lastHash = hash;

    var items = tabs.Select(t =>
    {
      var accent = ColorUtil.FromFlowColor(t.Color, Color.Black);
      return new ListItem(
          id: t.Id.ToString(),
          title: t.Name,
          accentColor: accent,
          kind: ListItemKind.Tab,
          badgeCount: null,
          isThumbnailLoading: false
      );
    }).ToList();

    SetItems(items);
    return true;
  }

  protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct)
  {
    var tabId = int.Parse(item.Id);
    var tabName = item.Title;

    _nav.Push(new EventsPage(_data, _nav, tabId, tabName));
    return Task.CompletedTask;
  }

  protected override Task OnBackAsync(CancellationToken ct)
  {
    _nav.Pop();
    return Task.CompletedTask;
  }
}
