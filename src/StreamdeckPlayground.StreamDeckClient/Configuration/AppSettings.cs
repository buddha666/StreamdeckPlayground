namespace StreamdeckPlayground.StreamDeckClient.Configuration;

/// <summary>
/// Configuration settings for the REST API connection.
/// These settings control how the Stream Deck client communicates with the web application.
/// </summary>
/// <remarks>
/// Example configuration in appsettings.json:
/// {
///   "ApiSettings": {
///     "BaseUrl": "http://localhost:5000",
///     "PollIntervalMs": 250
///   }
/// }
/// </remarks>
public class ApiSettings
{
    /// <summary>
    /// Base URL of the web API (e.g., "http://localhost:5000" or "https://myserver.com").
    /// Change this if your web app runs on a different port or machine.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Polling interval in milliseconds (how often to check for progress updates).
    /// Default: 250ms (4 times per second)
    /// 
    /// Lower values = more responsive but more CPU/network usage
    /// Higher values = less responsive but more efficient
    /// 
    /// Recommended range: 100ms - 1000ms
    /// </summary>
    public int PollIntervalMs { get; set; } = 250;
}

/// <summary>
/// Configuration settings for Stream Deck key mapping.
/// These settings define which physical keys on the Stream Deck are used for which functions.
/// </summary>
/// <remarks>
/// Stream Deck XL Key Layout (0-based indices):
/// Row 1 (top):    0,  1,  2,  3,  4,  5,  6,  7
/// Row 2:          8,  9, 10, 11, 12, 13, 14, 15
/// Row 3:         16, 17, 18, 19, 20, 21, 22, 23
/// Row 4 (bottom):24, 25, 26, 27, 28, 29, 30, 31
/// 
/// For Stream Deck (regular - 15 keys):
/// Row 1: 0, 1, 2, 3, 4
/// Row 2: 5, 6, 7, 8, 9
/// Row 3: 10, 11, 12, 13, 14
/// 
/// For Stream Deck Mini (6 keys):
/// Row 1: 0, 1, 2
/// Row 2: 3, 4, 5
/// </remarks>
public class StreamDeckSettings
{
    /// <summary>
    /// Index of the first key in the progress row (default: 0 for top-left key).
    /// The progress bar uses 8 consecutive keys starting from this index.
    /// 
    /// For Stream Deck XL: Use 0 (top row, keys 0-7)
    /// For Stream Deck regular: Use 0 (top row would be 0-4, but we need 8 keys total, so would span rows)
    /// For Stream Deck Mini: Not enough keys for 8-step progress
    /// </summary>
    public int ProgressRowStartIndex { get; set; } = 0;

    /// <summary>
    /// Index of the "Increase" button (default: 8, second row first key on Stream Deck XL).
    /// This key will display a mini progress bar and increment progress when pressed.
    /// </summary>
    public int IncreaseButtonIndex { get; set; } = 8;

    /// <summary>
    /// Index of the "Decrease" button (default: 9, second row second key on Stream Deck XL).
    /// This key will display a mini progress bar and decrement progress when pressed.
    /// </summary>
    public int DecreaseButtonIndex { get; set; } = 9;
}
