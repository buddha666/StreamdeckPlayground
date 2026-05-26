using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.UI.Navigation;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Colosseo.StreamDeckClient.UI.Pages;

public sealed class BanksPage : ScrollableListPage, IRefreshablePage
{
  private readonly IStreamDeckDataManager _data;
  private readonly INavigationService _nav;

  private int? _lastHash; // jednoduchá detekce změny

  public BanksPage(IStreamDeckDataManager data, INavigationService nav)
  {
    _data = data;
    _nav = nav;
  }

  public override string InfoLabel
  {
    get { return "BANKS"; }
  }

  public async Task<bool> RefreshAsync(CancellationToken ct)
  {
    var banks = await _data.GetBanksAsync(ct);

    // jednoduchý "hash": počty+verze+names (můžeš zlepšit)
    var hash = HashCode.Combine(
        banks.Count,
        banks.Count > 0 ? banks[0].Version : 0,
        banks.Count > 0 ? banks[^1].Version : 0
    );

    if (_lastHash.HasValue && _lastHash.Value == hash)
      return false;

    _lastHash = hash;

    var items = banks.Select(b =>
    {
      var accent = ColorUtil.FromFlowColor(b.Color, Color.DarkSlateGray);
      return new ListItem(
          id: b.Id.ToString(),
          title: b.Name,
          accentColor: accent,
          badgeCount: null,
          kind: ListItemKind.Bank,
          isThumbnailLoading: false
      );
    }).ToList();

    SetItems(items);
    return true;
  }

  protected override Task OnItemSelectedAsync(ListItem item, CancellationToken ct)
  {
    var bankId = int.Parse(item.Id);
    var bankName = item.Title;

    _nav.Push(new TabsPage(_data, _nav, bankId, bankName));
    return Task.CompletedTask;
  }

  protected override Task OnBackAsync(CancellationToken ct)
  {
    // root page - nic
    return Task.CompletedTask;
  }
}
