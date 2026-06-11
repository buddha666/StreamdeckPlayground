using Colosseo.StreamDeckClient.Device;
using Colosseo.StreamDeckClient.UI;
using Colosseo.StreamDeckClient.UI.Pages;
using Microsoft.Extensions.Logging;
using OpenMacroBoard.SDK;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrawingColor = System.Drawing.Color;

namespace Colosseo.StreamDeckClient.Rendering;

public sealed class ImageSharpStreamDeckRenderer : IStreamDeckRenderer
{
  private const int KeyWidth = 96;
  private const int KeyHeight = 96;

  private readonly IStreamDeckDeviceConnection _device;
  private readonly ILogger<ImageSharpStreamDeckRenderer> _logger;

  private readonly ConcurrentDictionary<int, string> _lastSig = new();

  private readonly Font _titleFont;
  private readonly Font _badgeFont;

  // ---- Layout tuning ----
  private const int BorderThickness = 2;
  private const int BorderInset = 2;

  private const int EventFrameInset = 2;
  private const int EventFrameThickness = 4;

  private const int TitleStripHeight = 36;
  private const int TitleStripHeightLong = 62;
  private const int TitleStripPad = 4;

  private const int BadgeWidth = 28;
  private const int BadgeHeight = 22;
  private const int BadgeInsetRight = 6;
  private const int BadgeInsetTop = 6;

  private const int InfoPagingHeight = 26;    // spodní část info key
  private const int InfoPad = 4;

  private const int TextWrapPad = 6;                // obecný padding pro text boxy
  private const int WordBreakChunkSize = 8;         // classic
  private const int WordBreakChunkSizeEvent = 10;   // event single-word breaking
  private const int MultilineTriggerLength = 10;
  private const int MaxTitleChars = 60;
  private const int SafeInset = BorderInset + BorderThickness;

  private const int ClassicAutoWrapTriggerLength = 11;
  private const int MaxLines = 3;
  private const int MaxCharsPerLine = 11;

  public ImageSharpStreamDeckRenderer(IStreamDeckDeviceConnection device, ILogger<ImageSharpStreamDeckRenderer> logger)
  {
    _device = device;
    _logger = logger;

    var family = TryGetFontFamily("Segoe UI") ?? TryGetFontFamily("Arial") ?? SystemFonts.Families.First();
    _titleFont = family.CreateFont(16, FontStyle.Bold);
    _badgeFont = family.CreateFont(14, FontStyle.Bold);
  }

  public Task RenderAsync(ScrollableListPage page, bool canGoBack, CancellationToken ct)
  {
    // Atomically snapshot AND clear dirty keys so background threads that set new dirty flags
    // after this point are not lost (they survive for the next tick).
    int[] dirtyKeys;
    try
    {
      dirtyKeys = page.TakeAndClearDirtyKeys();
    }
    catch
    {
      return Task.CompletedTask;
    }

    foreach (var keyIndex in dirtyKeys)
    {
      ct.ThrowIfCancellationRequested();

      // Grid pages (EventsGridPage) handle every slot themselves.
      if (page.TryGetItemByKeyIndex(keyIndex, out var gridItem))
      {
        if (page.KeyBack >= 0 && keyIndex == page.KeyBack)
        {
          RenderReserved(keyIndex, "BACK", enabled: canGoBack);
        }
        else if (gridItem == null)
        {
          RenderEmpty(keyIndex);
        }
        else
        {
          RenderListItem(keyIndex, gridItem);
        }
        continue;
      }

      // ---- standard scrollable-list path ----

      if (page.ShowNavigationControls && keyIndex == page.KeyScrollUp)
      {
        RenderReserved(keyIndex, "UP", enabled: page.CanScrollUp);
        continue;
      }

      if (page.ShowNavigationControls && keyIndex == page.KeyScrollDown)
      {
        RenderReserved(keyIndex, "DN", enabled: page.CanScrollDown);
        continue;
      }

      if (page.KeyBack >= 0 && keyIndex == page.KeyBack)
      {
        RenderReserved(keyIndex, "BACK", enabled: canGoBack);
        continue;
      }

      if (page.ShowNavigationControls && keyIndex == page.KeyInfo)
      {
        var pageNo = page.PageSize == 0 ? 1 : (page.Offset / page.PageSize) + 1;
        var pages = page.PageSize == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(page.ItemCount / (double)page.PageSize));
        RenderInfoKey(keyIndex, page.InfoLabel, pageNo, pages);
        continue;
      }

      int? itemIndex;
      try
      {
        itemIndex = page.TryMapKeyToItemIndex(keyIndex);
      }
      catch
      {
        RenderEmpty(keyIndex);
        continue;
      }

      if (itemIndex is null)
      {
        RenderEmpty(keyIndex);
        continue;
      }

      ListItem item;
      try
      {
        if (!page.TryGetItem(itemIndex.Value, out item))
        {
          RenderEmpty(keyIndex);
          continue;
        }
      }
      catch
      {
        RenderEmpty(keyIndex);
        continue;
      }

      RenderListItem(keyIndex, item);
    }

    return Task.CompletedTask;
  }

  private static FontFamily? TryGetFontFamily(string name)
      => SystemFonts.Families.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

  private void RenderReserved(int keyIndex, string text, bool enabled)
  {
    var sig = $"reserved:{text}:{enabled}";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);

    bool isBack = text == "BACK";
    if (isBack)
    {
      // Large left-pointing arrow on black background.
      // White when navigation is possible; dark-grey when disabled.
      var arrowColor = enabled ? new Rgba32(255, 255, 255) : new Rgba32(60, 60, 60);
      img.Mutate(ctx =>
      {
        ctx.Fill(new Rgba32(0, 0, 0));
        DrawBackArrow(ctx, arrowColor);
      });
    }
    else
    {
      var bg = enabled ? new Rgba32(20, 20, 20) : new Rgba32(5, 5, 5);
      var fg = enabled ? new Rgba32(240, 240, 240) : new Rgba32(80, 80, 80);

      img.Mutate(ctx =>
      {
        ctx.Fill(bg);
        ctx.DrawInsetBorder(fg, thickness: BorderThickness, inset: BorderInset, width: KeyWidth, height: KeyHeight);

        if (!string.IsNullOrWhiteSpace(text))
          DrawCenteredText(ctx, text, fg, _titleFont);
      });
    }

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  /// <summary>
  /// Draws a filled left-pointing arrow centred inside the 96×96 key area.
  /// The shape is a classic "chunky" arrow: wide triangle head + rectangular shaft.
  /// </summary>
  private static void DrawBackArrow(IImageProcessingContext ctx, Rgba32 color)
  {
    // Polygon points for a left-pointing arrow (96×96 canvas):
    //
    //          ←tip
    //          14,48
    //         /      \
    //       /          50,16  ─── shaft top ───  82,34
    //      /           50,34                     82,34
    //      \           50,62                     82,62
    //       \          50,80  ─── shaft bot ───  82,62
    //         \      /
    //          50,80  (bottom of arrowhead)
    //
    PointF[] pts =
    {
      new PointF(14, 48),   // arrow tip (leftmost point)
      new PointF(50, 16),   // top of arrowhead
      new PointF(50, 34),   // inner-top  (head → shaft junction)
      new PointF(82, 34),   // shaft top-right
      new PointF(82, 62),   // shaft bottom-right
      new PointF(50, 62),   // inner-bottom (shaft → head junction)
      new PointF(50, 80),   // bottom of arrowhead
    };
    ctx.FillPolygon(color, pts);
  }

  private void RenderEmpty(int keyIndex)
  {
    const string sig = "empty";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);
    img.Mutate(ctx => ctx.Fill(new Rgba32(0, 0, 0)));

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  private void RenderListItem(int keyIndex, ListItem item)
  {
    if (item.Kind is ListItemKind.StopOsdTop
        or ListItemKind.StopOsdMiddle
        or ListItemKind.StopOsdBottom
        or ListItemKind.StopAllActions
        or ListItemKind.HideOsdMenu)
    {
      RenderStopItem(keyIndex, item);
    }
    else if (item.IsEvent)
    {
      RenderEventItem(keyIndex, item);
    }
    else
    {
      RenderClassicItem(keyIndex, item);
    }
  }

  private void RenderStopItem(int keyIndex, ListItem item)
  {
    var sig = $"stop:{item.Kind}:{item.Title}";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);

    Rgba32 bg;
    if (item.Kind == ListItemKind.StopAllActions)
      bg = new Rgba32(160, 20, 20);    // dark red — stop all actions
    else if (item.Kind == ListItemKind.HideOsdMenu)
      bg = new Rgba32(80, 50, 0);     // dark amber — opens OSD sub-menu
    else
      bg = new Rgba32(100, 30, 0);    // dark orange — individual OSD hide buttons

    var border = new Rgba32(220, 80, 0);

    var title = (item.Title ?? "").Trim();
    title = TextUtils.BreakLongWords(title, WordBreakChunkSize);

    img.Mutate(ctx =>
    {
      ctx.Fill(bg);
      ctx.DrawInsetBorder(border, thickness: BorderThickness, inset: BorderInset, width: KeyWidth, height: KeyHeight);

      var textRect = Inset(new Rectangle(0, 0, KeyWidth, KeyHeight), SafeInset);
      if (ShouldClassicUseMultilineCentered(title))
      {
        title = BalanceTitleLines(title, MaxLines, MaxCharsPerLine);
        DrawManualMultilineCentered(ctx, title, new Rgba32(255, 255, 255, 255), _titleFont, textRect);
      }
      else
      {
        DrawCenteredText(ctx, title, new Rgba32(255, 255, 255, 255), _titleFont, textRect);
      }
    });

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  private void RenderEventItem(int keyIndex, ListItem item)
  {
    // IMPORTANT: include IsThumbnailLoading in signature so loading->loaded triggers rerender
    var sig = $"item:{item.Id}:{item.Title}:{item.AccentColor?.ToArgb()}:{item.BadgeCount}:{item.ThumbnailBytes?.Length}:{item.IsThumbnailLoading}";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);

    DrawingColor accent = item.AccentColor ?? DrawingColor.Black;
    var frameColor = new Rgba32(accent.R, accent.G, accent.B, 255);

    bool isComposite = item.Kind == ListItemKind.EventComposite;
    var inner = GetInnerRect();

    var isLoadingThumb = item.IsThumbnailLoading;
    var hasThumbBytes = item.ThumbnailBytes is { Length: > 0 };

    // load thumb if present
    Image<Rgba32>? thumb = null;
    if (hasThumbBytes)
    {
      try
      {
        thumb = Image.Load<Rgba32>(item.ThumbnailBytes);
        thumb.Mutate(t =>
        {
          t.Resize(new ResizeOptions
          {
            Size = new Size(inner.Width, inner.Height),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
          });
        });
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Thumbnail decode failed for {Id}", item.Id);
        thumb?.Dispose();
        thumb = null;
      }
    }

    img.Mutate(ctx =>
    {
      // Outer frame: white for composite events, accent colour for single events.
      ctx.Fill(isComposite ? new Rgba32(255, 255, 255, 255) : frameColor);
      // Black inner area (same for both variants).
      ctx.Fill(new Rgba32(0, 0, 0, 255), inner);

      // thumbnail OR loading placeholder (only background; no text to avoid clashing with title strip)
      if (thumb != null)
      {
        ctx.DrawImage(thumb, new Point(inner.X, inner.Y), 1f);
      }
      else if (isLoadingThumb)
      {
        ctx.Fill(new Rgba32(18, 18, 18, 255), inner);

        // subtle diagonal stripes (optional)
        for (int x = inner.X - inner.Height; x < inner.Right; x += 10)
        {
          ctx.DrawLines(
            new Rgba32(40, 40, 40, 255),
            2,
            new PointF(x, inner.Bottom),
            new PointF(x + inner.Height, inner.Y)
          );
        }
      }
      // else: thumb does not exist -> keep normal black background, no placeholder

      // ---- title overlay ----
      var title = (item.Title ?? "<no title>").Trim();

      // If thumbnail is still loading, show Loading in the strip instead of the real title
      var effectiveTitle = (thumb == null && isLoadingThumb) ? "Loading…" : title;

      var titleText = effectiveTitle;

      // Multi-word long title: prefer breaking by spaces into up to 3 lines.
      // Single-word titles use word-break only.
      if (titleText.Contains(' ') && titleText.Length >= MultilineTriggerLength)
      {
        titleText = BalanceTitleLinesEllipsizeLastLine(titleText, 3, MaxCharsPerLine);
      }
      else
      {
        titleText = TextUtils.BreakLongWords(titleText, WordBreakChunkSizeEvent);
      }

      if (titleText.Length > MaxTitleChars)
        titleText = TextUtils.Ellipsize(titleText, MaxTitleChars);

      var lineCount = CountLines(titleText);
      var stripHeight = lineCount >= 3 ? TitleStripHeightLong : TitleStripHeight;

      var rawStrip = new Rectangle(inner.X, inner.Y, inner.Width, stripHeight);
      var titleStrip = Inset(rawStrip, SafeInset);

      // stronger strip while loading, so it's readable over placeholder
      var stripAlpha = (thumb == null && isLoadingThumb) ? (byte)220 : (byte)140;
      ctx.Fill(new Rgba32(0, 0, 0, stripAlpha), rawStrip);

      if (titleText.Contains('\n'))
        DrawManualMultilineTopLeft(ctx, titleText, new Rgba32(255, 255, 255, 255), _badgeFont, titleStrip, pad: TitleStripPad);
      else
        DrawCenteredText(ctx, titleText, new Rgba32(255, 255, 255, 255), _badgeFont, titleStrip);

      // ---- badge (composite) ----
      if (item.BadgeCount is int n && n > 0)
      {
        var badgeRect = GetBadgeRect();
        ctx.Fill(new Rgba32(0, 0, 0, 180), badgeRect);
        DrawCenteredText(ctx, n.ToString(), new Rgba32(255, 255, 255, 255), _badgeFont, badgeRect);
      }
    });

    thumb?.Dispose();

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  private void RenderClassicItem(int keyIndex, ListItem item)
  {
    var sig = $"classic:{item.Id}:{item.Title}:{item.AccentColor?.ToArgb()}:{item.BadgeCount}";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);

    DrawingColor accent = item.AccentColor ?? DrawingColor.Black;
    var bg = new Rgba32(accent.R, accent.G, accent.B, 255);
    var textColor = PickTextColor(bg);

    var title = (item.Title ?? "<no title>").Trim();
    title = TextUtils.BreakLongWords(title, WordBreakChunkSize);
    if (title.Length > MaxTitleChars)
      title = TextUtils.Ellipsize(title, MaxTitleChars);

    img.Mutate(ctx =>
    {
      ctx.Fill(bg);

      // border
      ctx.DrawInsetBorder(new Rgba32(0, 0, 0, 255), thickness: 4, inset: 4, width: KeyWidth, height: KeyHeight);

      var textRect = Inset(new Rectangle(0, 0, KeyWidth, KeyHeight), SafeInset);

      if (ShouldClassicUseMultilineCentered(title))
      {
        title = BalanceTitleLines(title, MaxLines, MaxCharsPerLine);
        DrawManualMultilineCentered(ctx, title, textColor, _titleFont, textRect);
      }
      else
      {
        DrawCenteredText(ctx, title, textColor, _titleFont, textRect);
      }
    });

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  private static Rectangle GetInnerRect()
  {
    var m = EventFrameInset + EventFrameThickness;
    return new Rectangle(m, m, KeyWidth - (m * 2), KeyHeight - (m * 2));
  }

  private static Rectangle GetBadgeRect()
    => new Rectangle(KeyWidth - (BadgeInsetRight + BadgeWidth), BadgeInsetTop, BadgeWidth, BadgeHeight);

  private static void DrawCenteredText(IImageProcessingContext ctx, string text, Rgba32 color, Font font)
      => DrawCenteredText(ctx, text, color, font, new Rectangle(0, 0, KeyWidth, KeyHeight));

  private static void DrawCenteredText(IImageProcessingContext ctx, string text, Rgba32 color, Font font, Rectangle bounds)
  {
    var options = new TextOptions(font)
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      Origin = new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f),
      WrappingLength = bounds.Width - (TextWrapPad * 2)
    };

    ctx.DrawText(options, text, color);
  }

  private void RenderInfoKey(int keyIndex, string label, int pageNo, int pages)
  {
    label = (label ?? "").Trim();

    label = TextUtils.BreakLongWords(label, WordBreakChunkSize);
    if (label.Length > MaxTitleChars)
      label = TextUtils.Ellipsize(label, MaxTitleChars);

    var pageText = $"{pageNo}/{pages}";

    var sig = $"info:{label}:{pageText}";
    if (_lastSig.TryGetValue(keyIndex, out var last) && last == sig) return;

    using var img = new Image<Rgba32>(KeyWidth, KeyHeight);
    var bg = new Rgba32(5, 5, 5);
    var fg = new Rgba32(240, 240, 240);

    img.Mutate(ctx =>
    {
      ctx.Fill(bg);
      ctx.DrawInsetBorder(fg, thickness: BorderThickness, inset: BorderInset, width: KeyWidth, height: KeyHeight);

      var pageRect = new Rectangle(0, KeyHeight - InfoPagingHeight, KeyWidth, InfoPagingHeight);
      DrawCenteredText(ctx, pageText, new Rgba32(255, 255, 255, 255), _titleFont, pageRect);

      if (!string.IsNullOrWhiteSpace(label))
      {
        var rawLabelRect = new Rectangle(0, 0, KeyWidth, KeyHeight - InfoPagingHeight);
        var labelRect = Inset(rawLabelRect, SafeInset);

        if (label.Contains('\n'))
          DrawManualMultilineTopLeft(ctx, label, new Rgba32(200, 200, 200, 255), _badgeFont, labelRect, pad: InfoPad);
        else
          DrawTextInBoxTopLeft(ctx, label, new Rgba32(200, 200, 200, 255), _badgeFont, labelRect, pad: InfoPad);
      }
    });

    Send(keyIndex, img);
    _lastSig[keyIndex] = sig;
  }

  private void Send(int keyIndex, Image<Rgba32> img)
  {
    var bgr24 = new byte[KeyWidth * KeyHeight * 3];
    var i = 0;

    img.ProcessPixelRows(accessor =>
    {
      for (int y = 0; y < KeyHeight; y++)
      {
        var row = accessor.GetRowSpan(y);
        for (int x = 0; x < KeyWidth; x++)
        {
          var p = row[x];
          bgr24[i++] = p.B;
          bgr24[i++] = p.G;
          bgr24[i++] = p.R;
        }
      }
    });

    _device.SetKeyBitmapBgr24(keyIndex, KeyWidth, KeyHeight, bgr24);
  }

  private static void DrawTextInBoxTopLeft(IImageProcessingContext ctx, string text, Rgba32 color, Font font, Rectangle bounds, int pad = TextWrapPad)
  {
    var options = new TextOptions(font)
    {
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Top,
      Origin = new PointF(bounds.Left + pad, bounds.Top + pad),
      WrappingLength = bounds.Width - (pad * 2)
    };

    ctx.DrawText(options, text, color);
  }

  private static void DrawManualMultilineTopLeft(IImageProcessingContext ctx, string text, Rgba32 color, Font font, Rectangle bounds, int pad = TextWrapPad)
  {
    var options = new TextOptions(font)
    {
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Top,
      Origin = new PointF(bounds.Left + pad, bounds.Top + pad),
      WrappingLength = 0
    };

    ctx.DrawText(options, text, color);
  }

  private static Rgba32 PickTextColor(Rgba32 bg)
  {
    var luma = (0.2126f * bg.R) + (0.7152f * bg.G) + (0.0722f * bg.B);
    return luma > 140 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
  }

  private static void DrawManualMultilineCentered(IImageProcessingContext ctx, string text, Rgba32 color, Font font, Rectangle bounds)
  {
    var lines = text.Split('\n').Length;
    var lineHeight = font.Size * 1.25f;
    var blockHeight = lines * lineHeight;

    var centerX = bounds.Left + (bounds.Width / 2f);
    var startY = bounds.Top + Math.Max(0f, (bounds.Height - blockHeight) / 2f);

    var options = new TextOptions(font)
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Top,
      Origin = new PointF(centerX, startY),
      WrappingLength = 0
    };

    ctx.DrawText(options, text, color);
  }

  private static bool ShouldClassicUseMultilineCentered(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return false;
    if (text.Contains('\n')) return true;

    if (text.Contains(' ') && text.Length >= ClassicAutoWrapTriggerLength) return true;

    return false;
  }

  private static string BalanceTitleLines(string text, int maxLines, int maxCharsPerLine)
  {
    text = (text ?? "").Trim();
    if (text.Length == 0) return text;
    if (text.Contains('\n')) return text;

    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length <= 1) return text;

    var lines = new System.Collections.Generic.List<string>(maxLines);
    var current = "";

    foreach (var w in words)
    {
      if (current.Length == 0)
      {
        current = w;
        continue;
      }

      var candidate = current + " " + w;

      if (candidate.Length <= maxCharsPerLine)
      {
        current = candidate;
        continue;
      }

      lines.Add(current);

      if (lines.Count >= maxLines)
        break;

      current = w;

      if (lines.Count == maxLines - 1 && current.Length > maxCharsPerLine)
      {
        if (maxCharsPerLine >= 2)
          current = current.Substring(0, maxCharsPerLine - 1) + "…";
        else
          current = "…";
        break;
      }
    }

    if (lines.Count < maxLines && current.Length > 0)
      lines.Add(current);

    if (lines.Count == maxLines)
    {
      var last = lines[^1];
      if (last.Length > maxCharsPerLine)
      {
        if (maxCharsPerLine >= 2)
          lines[^1] = last.Substring(0, maxCharsPerLine - 1) + "…";
        else
          lines[^1] = "…";
      }
    }

    return string.Join("\n", lines);
  }

  private static string BalanceTitleLinesEllipsizeLastLine(string text, int maxLines, int maxCharsPerLine)
  {
    text = (text ?? "").Trim();
    if (text.Length == 0) return text;
    if (text.Contains('\n')) return text;

    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length <= 1) return text;

    var lines = new System.Collections.Generic.List<string>(maxLines);
    var current = "";
    var truncated = false;

    foreach (var w in words)
    {
      if (current.Length == 0)
      {
        current = w;
        continue;
      }

      var candidate = current + " " + w;

      if (candidate.Length <= maxCharsPerLine)
      {
        current = candidate;
        continue;
      }

      lines.Add(current);

      if (lines.Count >= maxLines)
      {
        truncated = true;
        current = "";
        break;
      }

      current = w;

      if (current.Length > maxCharsPerLine)
      {
        truncated = true;
        if (maxCharsPerLine >= 2)
          current = current.Substring(0, maxCharsPerLine - 1) + "…";
        else
          current = "…";
        if (lines.Count >= maxLines - 1)
          break;
      }
    }

    if (lines.Count < maxLines && current.Length > 0)
      lines.Add(current);

    if (lines.Count > maxLines)
    {
      truncated = true;
      lines = lines.Take(maxLines).ToList();
    }

    if (lines.Count > 0)
    {
      var lastIndex = lines.Count - 1;
      var last = lines[lastIndex].Trim();

      if (last.Length > maxCharsPerLine)
      {
        truncated = true;
        if (maxCharsPerLine >= 2)
          last = last.Substring(0, maxCharsPerLine - 1);
        else
          last = "";
      }

      if (truncated)
      {
        if (last.Length >= maxCharsPerLine && maxCharsPerLine >= 1)
          last = last.Substring(0, Math.Max(0, maxCharsPerLine - 1));

        last = (last.Length == 0) ? "…" : last + "…";
      }

      lines[lastIndex] = last;
    }

    return string.Join("\n", lines);
  }

  private static int CountLines(string text)
  {
    if (string.IsNullOrEmpty(text)) return 0;
    return 1 + text.Count(c => c == '\n');
  }

  private static Rectangle Inset(Rectangle r, int pad)
    => new Rectangle(r.X + pad, r.Y + pad, r.Width - (pad * 2), r.Height - (pad * 2));
}