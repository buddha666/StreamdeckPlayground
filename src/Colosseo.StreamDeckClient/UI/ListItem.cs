using DrawingColor = System.Drawing.Color;

namespace Colosseo.StreamDeckClient.UI;

public sealed class ListItem
{
  public string Id { get; }
  public string Title { get; }
  public DrawingColor? AccentColor { get; }
  public int? BadgeCount { get; }
  public byte[] ThumbnailBytes { get; }
  public bool IsEvent => Kind is ListItemKind.EventSingle or ListItemKind.EventComposite;
  public ListItemKind Kind { get; set; }

  public bool IsThumbnailLoading { get; }

  public ListItem(string id, string title, DrawingColor? accentColor, int? badgeCount, ListItemKind kind, bool isThumbnailLoading, byte[] thumbnailBytes = null)
  {
    Id = id;
    Title = title;
    AccentColor = accentColor;
    BadgeCount = badgeCount;
    ThumbnailBytes = thumbnailBytes;
    Kind = kind;
    IsThumbnailLoading = isThumbnailLoading;
  }
}
