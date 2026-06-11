namespace Colosseo.StreamDeckClient.UI
{
  public enum ListItemKind
  {
    Unknown = 0,
    Bank = 1,
    Tab = 2,
    EventSingle = 3,
    EventComposite = 4,
    StopOsdTop = 5,
    StopOsdMiddle = 6,
    StopOsdBottom = 7,
    StopAllActions = 8,
    /// <summary>
    /// Navigation button shown in the stop column on 3-row devices.
    /// Pressing it opens the <c>HideOsdSubPage</c> where the individual OSD hide buttons are displayed.
    /// </summary>
    HideOsdMenu = 9,
  }
}
