using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Device;
using Colosseo.StreamDeckClient.UI.Navigation;
using Monogram.Sport.FlowBLL.Enums;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

/// <summary>
/// Sub-page shown on 3-row devices when the user presses the HIDE OSD navigation button.
/// Displays HIDE OSD TOP / MIDDLE / BOTTOM in the stop column plus a BACK button in the
/// bottom-left corner. Pressing any of the three OSD buttons executes the action and
/// automatically navigates back to the previous page.
/// </summary>
public sealed class HideOsdSubPage : ScrollableListPage
{
    private readonly IStreamDeckDataManager _data;
    private readonly INavigationService _nav;

    // All slots managed directly (no scroll list).
    private readonly ListItem[] _slots;

    public HideOsdSubPage(
        IStreamDeckDataManager data,
        INavigationService nav,
        StreamDeckOptions cfg,
        DeviceLayout layout = null)
    {
        Layout = layout ?? DeviceLayout.Default;
        _slots = new ListItem[Layout.TotalKeys];

        _data = data;
        _nav = nav;

        // Populate the stop column with the three individual OSD buttons.
        int stopCol = cfg.ShowStopButtons ? Layout.Columns - 1 : -1;
        if (stopCol >= 0)
        {
            _slots[0 * Layout.Columns + stopCol] = MakeStopItem(ListItemKind.StopOsdTop,    "HIDE OSD TOP");
            _slots[1 * Layout.Columns + stopCol] = MakeStopItem(ListItemKind.StopOsdMiddle, "HIDE OSD MIDDLE");
            _slots[2 * Layout.Columns + stopCol] = MakeStopItem(ListItemKind.StopOsdBottom, "HIDE OSD BOTTOM");
        }
        // Everything else (including the BACK key slot) remains null so the renderer
        // draws it as BACK (for KeyBack) or as empty (for all other null slots).
    }

    // BACK button at bottom-left.
    public override int KeyBack => Layout.KeyBackBottomLeft;

    // ---- ScrollableListPage overrides ----

    public override bool TryGetItemByKeyIndex(int keyIndex, out ListItem item)
    {
        item = (keyIndex >= 0 && keyIndex < _slots.Length) ? _slots[keyIndex] : null;
        return true;
    }

    public override async Task OnKeyDownAsync(int keyIndex, CancellationToken ct)
    {
        if (keyIndex == KeyBack)
        {
            _nav.Pop();
            return;
        }

        ListItem item = (keyIndex >= 0 && keyIndex < _slots.Length) ? _slots[keyIndex] : null;
        if (item == null) return;

        switch (item.Kind)
        {
            case ListItemKind.StopOsdTop:
                await _data.HideOsd(SystemEventTypeCode.OsdTop, ct);
                _nav.Pop();
                break;
            case ListItemKind.StopOsdMiddle:
                await _data.HideOsd(SystemEventTypeCode.OsdMiddle, ct);
                _nav.Pop();
                break;
            case ListItemKind.StopOsdBottom:
                await _data.HideOsd(SystemEventTypeCode.OsdBottom, ct);
                _nav.Pop();
                break;
        }
    }

    // Required by abstract base but never called — this page handles all keys itself.
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
}
