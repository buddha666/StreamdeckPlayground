using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;

namespace Colosseo.StreamDeckClient.Rendering;

/// <summary>
/// Builds a 2×2 composite thumbnail from up to four child thumbnail byte arrays.
/// Empty quadrants are filled with black.  The four quadrants are separated by a
/// one-pixel-wide white line.
/// </summary>
public static class ThumbnailCompositor
{
    private const int ImageSize = 96;  // matches KeyWidth / KeyHeight in the renderer
    private const int SepSize   = 1;   // thin white separator line

    // Cell dimensions so that two cells + 1 separator exactly fill ImageSize.
    private static readonly int CellA = (ImageSize - SepSize) / 2;               // 47
    private static readonly int CellB = ImageSize - SepSize - CellA;             // 48

    // Quadrant layout (0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right)
    private static readonly Rectangle[] Quads =
    [
        new Rectangle(0,          0,          CellA, CellA),
        new Rectangle(CellA + SepSize, 0,     CellB, CellA),
        new Rectangle(0,          CellA + SepSize, CellA, CellB),
        new Rectangle(CellA + SepSize, CellA + SepSize, CellB, CellB),
    ];

    /// <summary>
    /// Composites up to four child thumbnail byte arrays (PNG/JPEG/…) into a single
    /// 96×96 PNG arranged in a 2×2 grid.  Quadrants without a thumbnail are black.
    /// </summary>
    /// <param name="childBytes">
    /// Ordered list of raw image bytes.  Entries may be <c>null</c> or empty; the
    /// corresponding quadrant is then left black.  Only the first four entries are used.
    /// </param>
    public static byte[] Build(IReadOnlyList<byte[]> childBytes)
    {
        using var composite = new Image<Rgba32>(ImageSize, ImageSize);

        composite.Mutate(ctx =>
        {
            // Background: black
            ctx.Fill(new Rgba32(0, 0, 0));

            // Separator lines (white)
            ctx.Fill(new Rgba32(255, 255, 255),
                new Rectangle(CellA, 0,    SepSize, ImageSize)); // vertical
            ctx.Fill(new Rgba32(255, 255, 255),
                new Rectangle(0,    CellA, ImageSize, SepSize)); // horizontal

            // Draw each quadrant
            int count = System.Math.Min(childBytes?.Count ?? 0, 4);
            for (int i = 0; i < count; i++)
            {
                var bytes = childBytes![i];
                if (bytes == null || bytes.Length == 0) continue;

                Image<Rgba32>? tile = null;
                try
                {
                    tile = Image.Load<Rgba32>(bytes);
                    var quad = Quads[i];
                    tile.Mutate(t => t.Resize(new ResizeOptions
                    {
                        Size     = new Size(quad.Width, quad.Height),
                        Mode     = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center,
                    }));
                    ctx.DrawImage(tile, new Point(quad.X, quad.Y), opacity: 1f);
                }
                catch
                {
                    // Ignore bad/corrupt child thumbnail; quadrant stays black.
                }
                finally
                {
                    tile?.Dispose();
                }
            }
        });

        using var ms = new MemoryStream();
        composite.SaveAsPng(ms);
        return ms.ToArray();
    }
}
