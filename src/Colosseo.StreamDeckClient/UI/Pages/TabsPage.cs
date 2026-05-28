using Colosseo.StreamDeckClient.Config;
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
  private readonly StreamDeckClientConfig _cfg;

  private int? _lastHash;

  public TabsPage(IStreamDeckDataManager data, INavigationService nav, int bankId, string bankName, StreamDeckClientConfig cfg)
  {
    _data = data;
    _nav = nav;
    _bankId = bankId;
    _bankName = bankName ?? "";
    _cfg = cfg;
  }

  public override string InfoLabel
  {
    get { return _bankName.Length > 0 ? ("TABS: " + _bankName) : "TABS"; }
  }

  // BACK button is in the bottom-left corner (x=0, y=3 → keyIndex=24)
  public override int KeyBack => 24;

  public async Task<bool> RefreshAsync(CancellationToken ct)
  {
    var tabs = await _data.GetTabsAsync(_bankId, ct);

    // Filter out QuickTab items — they are shown inside EventsGridPage
    var visibleTabs = tabs.Where(t => !t.IsQuick).ToList();

    var hash = HashCode.Combine(
        visibleTabs.Count,
        visibleTabs.Count > 0 ? visibleTabs[0].Version : 0,
        visibleTabs.Count > 0 ? visibleTabs[^1].Version : 0
    );

    if (_lastHash.HasValue && _lastHash.Value == hash)
      return false;

    _lastHash = hash;

    var items = visibleTabs.Select(t =>
    {
      var accent = ColorUtil.FromFlowColor(t.Color, Color.Black);
      return new ListItem(
                    id: t.Id.ToString(),
                    title: t.Name,
                    accentColor: accent,
                    kind: ListItemKind.Tab,
                    badgeCount: null,
                    isThumbnailLoading: false,
                    isQuickTab: t.IsQuick
      );
    }).ToList();

    SetItems(items);
    return true;
  }

  protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct)
  {
    var tabId = int.Parse(item.Id);
    var tabName = item.Title;

    _nav.Push(new EventsGridPage(_data, _nav, _bankId, tabId, tabName, _cfg));
    return Task.CompletedTask;
  }

  protected override Task OnBackAsync(CancellationToken ct)
  {
    _nav.Pop();
    return Task.CompletedTask;
  }
}
