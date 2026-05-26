using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Colosseo.StreamDeckClient.Rendering;

public static class ImageSharpPrimitives
{
  public static void DrawInsetBorder(this IImageProcessingContext ctx, Rgba32 color, int thickness, int inset, int width, int height)
  {
    // top
    ctx.Fill(color, new Rectangle(inset, inset, width - 2 * inset, thickness));
    // bottom
    ctx.Fill(color, new Rectangle(inset, height - inset - thickness, width - 2 * inset, thickness));
    // left
    ctx.Fill(color, new Rectangle(inset, inset, thickness, height - 2 * inset));
    // right
    ctx.Fill(color, new Rectangle(width - inset - thickness, inset, thickness, height - 2 * inset));
  }
}
