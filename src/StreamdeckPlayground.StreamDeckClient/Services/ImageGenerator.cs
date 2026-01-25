using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace StreamdeckPlayground.StreamDeckClient.Services;

/// <summary>
/// Service for generating images for Stream Deck keys.
/// Uses SixLabors.ImageSharp to create images at runtime.
/// </summary>
/// <remarks>
/// Stream Deck Key Image Requirements:
/// - Stream Deck XL: 96x96 pixels
/// - Stream Deck (regular): 72x72 pixels  
/// - Stream Deck Mini: 80x80 pixels
/// - Format: JPEG or BMP (we use BMP for simplicity)
/// - Color: RGB24
/// 
/// This service creates:
/// 1. Solid color blocks for the progress bar (top row)
/// 2. Mini progress bars for increment/decrement buttons
/// </remarks>
public class ImageGenerator
{
    private readonly ILogger<ImageGenerator> _logger;
    
    // Stream Deck XL key size (96x96 pixels)
    // For other models, change these values:
    // - Stream Deck (regular): 72x72
    // - Stream Deck Mini: 80x80
    private const int KeyWidth = 96;
    private const int KeyHeight = 96;

    public ImageGenerator(ILogger<ImageGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a solid color image for a progress bar segment.
    /// Used for the top row keys that light up as progress increases.
    /// </summary>
    /// <param name="isActive">True = filled (green), False = empty (dark gray)</param>
    /// <returns>96x96 pixel image as byte array</returns>
    public byte[] CreateProgressBlockImage(bool isActive)
    {
        // Choose color based on whether this segment is active
        Color backgroundColor = isActive 
            ? Color.FromRgb(34, 139, 34)   // Forest Green for active segments
            : Color.FromRgb(40, 40, 40);   // Dark Gray for inactive segments

        using (Image<Rgb24> image = new Image<Rgb24>(KeyWidth, KeyHeight))
        {
            // Fill entire image with the background color
            image.Mutate(ctx => ctx.Fill(backgroundColor));

            // Add a subtle border to make individual keys distinguishable
            Color borderColor = Color.FromRgb(80, 80, 80);
            image.Mutate(ctx => ctx.Draw(borderColor, 2, new RectangleF(0, 0, KeyWidth, KeyHeight)));

            // Convert image to byte array
            return ImageToByteArray(image);
        }
    }

    /// <summary>
    /// Creates an image with a mini progress bar for increment/decrement buttons.
    /// Shows current progress visually on the button itself.
    /// </summary>
    /// <param name="currentStep">Current step (0-8)</param>
    /// <param name="maxSteps">Maximum steps (8)</param>
    /// <param name="isIncrease">True for increase button (+), False for decrease button (-)</param>
    /// <returns>96x96 pixel image as byte array</returns>
    public byte[] CreateControlButtonImage(int currentStep, int maxSteps, bool isIncrease)
    {
        using (Image<Rgb24> image = new Image<Rgb24>(KeyWidth, KeyHeight))
        {
            // Background color
            Color backgroundColor = Color.FromRgb(40, 40, 40);
            image.Mutate(ctx => ctx.Fill(backgroundColor));

            // Calculate progress percentage
            float progressPercent = maxSteps > 0 ? (float)currentStep / maxSteps : 0;

            // Draw mini progress bar in the middle of the key
            int barHeight = 15;
            int barWidth = KeyWidth - 20; // Leave 10px margin on each side
            int barX = 10;
            int barY = (KeyHeight - barHeight) / 2;

            // Draw progress bar background (dark)
            Color barBackgroundColor = Color.FromRgb(60, 60, 60);
            image.Mutate(ctx => ctx.Fill(
                barBackgroundColor,
                new RectangleF(barX, barY, barWidth, barHeight)
            ));

            // Draw filled portion of progress bar
            int filledWidth = (int)(barWidth * progressPercent);
            if (filledWidth > 0)
            {
                Color fillColor = Color.FromRgb(34, 139, 34); // Green
                image.Mutate(ctx => ctx.Fill(
                    fillColor,
                    new RectangleF(barX, barY, filledWidth, barHeight)
                ));
            }

            // Draw border around progress bar
            Color borderColor = Color.FromRgb(120, 120, 120);
            image.Mutate(ctx => ctx.Draw(
                borderColor,
                2,
                new RectangleF(barX, barY, barWidth, barHeight)
            ));

            // Draw symbol (+ or -) above the progress bar
            DrawSymbol(image, isIncrease);

            return ImageToByteArray(image);
        }
    }

    /// <summary>
    /// Draws a + or - symbol on the image.
    /// Simple geometric shapes using rectangles.
    /// </summary>
    private void DrawSymbol(Image<Rgb24> image, bool isPlus)
    {
        Color symbolColor = Color.FromRgb(200, 200, 200);
        int symbolSize = 30;
        int symbolThickness = 6;
        int symbolY = 20; // Position above the progress bar

        if (isPlus)
        {
            // Draw + symbol (horizontal + vertical rectangles)
            // Horizontal bar
            image.Mutate(ctx => ctx.Fill(
                symbolColor,
                new RectangleF(
                    (KeyWidth - symbolSize) / 2,
                    symbolY + (symbolSize - symbolThickness) / 2,
                    symbolSize,
                    symbolThickness
                )
            ));
            
            // Vertical bar
            image.Mutate(ctx => ctx.Fill(
                symbolColor,
                new RectangleF(
                    (KeyWidth - symbolThickness) / 2,
                    symbolY,
                    symbolThickness,
                    symbolSize
                )
            ));
        }
        else
        {
            // Draw - symbol (horizontal rectangle)
            image.Mutate(ctx => ctx.Fill(
                symbolColor,
                new RectangleF(
                    (KeyWidth - symbolSize) / 2,
                    symbolY + (symbolSize - symbolThickness) / 2,
                    symbolSize,
                    symbolThickness
                )
            ));
        }
    }

    /// <summary>
    /// Converts an ImageSharp Image to a byte array.
    /// Stream Deck requires images as byte arrays.
    /// </summary>
    /// <remarks>
    /// We use BMP format because:
    /// 1. No compression = fast encoding
    /// 2. Lossless quality
    /// 3. Simple format
    /// 
    /// Alternative: Use JPEG for smaller file sizes (but lossy compression)
    /// </remarks>
    private byte[] ImageToByteArray(Image<Rgb24> image)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            // Encode as BMP format
            image.SaveAsBmp(ms);
            return ms.ToArray();
        }
    }
}
