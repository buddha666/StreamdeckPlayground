using Microsoft.Extensions.Logging;
using OpenMacroBoard.SDK;
using StreamDeckSharp;
using StreamdeckPlayground.Shared;
using StreamdeckPlayground.StreamDeckClient.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace StreamdeckPlayground.StreamDeckClient.Services;

/// <summary>
/// Main service that coordinates Stream Deck interactions.
/// Handles device connection, key events, polling, and visual updates.
/// </summary>
/// <remarks>
/// This service orchestrates all Stream Deck functionality:
/// 1. Discovers and connects to Stream Deck XL device
/// 2. Polls the API for progress changes
/// 3. Updates key visuals when progress changes
/// 4. Handles key press events (increase/decrease buttons)
/// 
/// Thread Safety:
/// - Uses locks to prevent concurrent Stream Deck access
/// - API polling and event handling may happen on different threads
/// </remarks>
public class StreamDeckController : IDisposable
{
    private readonly ProgressApiClient _apiClient;
    private readonly ImageGenerator _imageGenerator;
    private readonly StreamDeckSettings _settings;
    private readonly ILogger<StreamDeckController> _logger;
    
    private IMacroBoard? _streamDeck;
    private ProgressState? _lastKnownState;
    private readonly object _deviceLock = new object();
    private CancellationTokenSource? _pollCancellation;
    
    // Stream Deck XL key dimensions
    private const int KeyWidth = 96;
    private const int KeyHeight = 96;

    public StreamDeckController(
        ProgressApiClient apiClient,
        ImageGenerator imageGenerator,
        StreamDeckSettings settings,
        ILogger<StreamDeckController> logger)
    {
        _apiClient = apiClient;
        _imageGenerator = imageGenerator;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Discovers and connects to a Stream Deck device.
    /// </summary>
    /// <returns>True if successfully connected, false otherwise</returns>
    public bool Connect()
    {
        try
        {
            _logger.LogInformation("Searching for Stream Deck devices...");
            
            // StreamDeckSharp.StreamDeck.OpenDevice() discovers and connects to the first available device
            // It supports: Stream Deck XL, Stream Deck (regular), Stream Deck Mini
            _streamDeck = StreamDeck.OpenDevice();
            
            if (_streamDeck == null)
            {
                _logger.LogError("No Stream Deck device found. Please ensure:");
                _logger.LogError("  1. Stream Deck is connected via USB");
                _logger.LogError("  2. Device drivers are installed");
                _logger.LogError("  3. You have permissions to access the device (may need admin/sudo)");
                return false;
            }

            _logger.LogInformation("Connected to Stream Deck");
            _logger.LogInformation("  Key Count: {Count}", _streamDeck.Keys.Count);
            _logger.LogInformation("  Key Size: {Width}x{Height}", 
                _streamDeck.Keys.Area.Width, _streamDeck.Keys.Area.Height);

            // Validate that we have enough keys for our configuration
            int requiredKeys = Math.Max(
                _settings.ProgressRowStartIndex + 8,
                Math.Max(_settings.IncreaseButtonIndex, _settings.DecreaseButtonIndex) + 1
            );
            
            if (_streamDeck.Keys.Count < requiredKeys)
            {
                _logger.LogWarning(
                    "Stream Deck only has {ActualKeys} keys, but configuration requires {RequiredKeys} keys",
                    _streamDeck.Keys.Count, requiredKeys);
                _logger.LogWarning("Please adjust key indices in appsettings.json");
            }

            // Subscribe to key press events
            // This event fires whenever a user presses a key on the Stream Deck
            _streamDeck.KeyStateChanged += OnKeyStateChanged;

            _logger.LogInformation("Stream Deck initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Stream Deck");
            return false;
        }
    }

    /// <summary>
    /// Starts polling the API for progress updates.
    /// Updates the Stream Deck visuals whenever progress changes.
    /// </summary>
    /// <param name="pollIntervalMs">How often to poll (milliseconds)</param>
    public void StartPolling(int pollIntervalMs)
    {
        _pollCancellation = new CancellationTokenSource();
        
        // Start background task for polling
        Task.Run(async () => await PollApiLoop(pollIntervalMs, _pollCancellation.Token));
        
        _logger.LogInformation("Started polling API every {Interval}ms", pollIntervalMs);
    }

    /// <summary>
    /// Stops polling the API.
    /// </summary>
    public void StopPolling()
    {
        _pollCancellation?.Cancel();
        _logger.LogInformation("Stopped polling API");
    }

    /// <summary>
    /// Background loop that polls the API for progress updates.
    /// Runs continuously until cancelled.
    /// </summary>
    private async Task PollApiLoop(int intervalMs, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get current progress from API
                ProgressState? currentState = await _apiClient.GetProgressAsync();
                
                if (currentState != null)
                {
                    // Check if state has changed since last poll
                    if (_lastKnownState == null || 
                        _lastKnownState.Step != currentState.Step)
                    {
                        _logger.LogInformation(
                            "Progress changed: {OldStep} -> {NewStep} ({Percent}%)",
                            _lastKnownState?.Step ?? -1,
                            currentState.Step,
                            currentState.Percent);
                        
                        // Update Stream Deck visuals
                        UpdateStreamDeckVisuals(currentState);
                        _lastKnownState = currentState;
                    }
                }

                // Wait before next poll
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop");
                await Task.Delay(intervalMs, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Updates all Stream Deck key visuals based on current progress.
    /// </summary>
    /// <param name="state">Current progress state</param>
    private void UpdateStreamDeckVisuals(ProgressState state)
    {
        if (_streamDeck == null) return;

        lock (_deviceLock)
        {
            try
            {
                // Update progress bar (top row - 8 keys)
                // Keys light up from left to right based on current step
                for (int i = 0; i < 8; i++)
                {
                    int keyIndex = _settings.ProgressRowStartIndex + i;
                    
                    // Key is active if its index is less than current step
                    // Example: If step=3, keys 0,1,2 are active (3 keys lit)
                    bool isActive = i < state.Step;
                    
                    byte[] image = _imageGenerator.CreateProgressBlockImage(isActive);
                    _streamDeck.SetKeyBitmap(keyIndex, KeyBitmap.Create.FromBgr24Array(KeyWidth, KeyHeight, ConvertToRgb(image)));
                }

                // Update increase button
                {
                    byte[] image = _imageGenerator.CreateControlButtonImage(
                        state.Step, state.Max, isIncrease: true);
                    _streamDeck.SetKeyBitmap(
                        _settings.IncreaseButtonIndex, 
                        KeyBitmap.Create.FromBgr24Array(KeyWidth, KeyHeight, ConvertToRgb(image)));
                }

                // Update decrease button
                {
                    byte[] image = _imageGenerator.CreateControlButtonImage(
                        state.Step, state.Max, isIncrease: false);
                    _streamDeck.SetKeyBitmap(
                        _settings.DecreaseButtonIndex, 
                        KeyBitmap.Create.FromBgr24Array(KeyWidth, KeyHeight, ConvertToRgb(image)));
                }

                _logger.LogDebug("Updated Stream Deck visuals for step {Step}", state.Step);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Stream Deck visuals");
            }
        }
    }

    /// <summary>
    /// Event handler for Stream Deck key presses.
    /// Called whenever a key state changes (pressed or released).
    /// </summary>
    /// <remarks>
    /// KeyEventArgs properties:
    /// - Key: Index of the key that changed (0-based)
    /// - IsDown: True if pressed, False if released
    /// 
    /// We only respond to key press (IsDown=true), not release.
    /// </remarks>
    private void OnKeyStateChanged(object? sender, KeyEventArgs e)
    {
        // Only respond to key press (down), not release (up)
        if (!e.IsDown)
            return;

        _logger.LogInformation("Key pressed: index={KeyIndex}", e.Key);

        // Check which button was pressed
        if (e.Key == _settings.IncreaseButtonIndex)
        {
            HandleIncreaseButton();
        }
        else if (e.Key == _settings.DecreaseButtonIndex)
        {
            HandleDecreaseButton();
        }
        else
        {
            _logger.LogDebug("Key {Key} is not mapped to any action", e.Key);
        }
    }

    /// <summary>
    /// Handles the increase button press.
    /// Calls the API to increment progress.
    /// </summary>
    private void HandleIncreaseButton()
    {
        _logger.LogInformation("Increase button pressed");
        
        // Fire and forget - we don't wait for the result
        // The polling loop will pick up the change
        _ = Task.Run(async () =>
        {
            ProgressState? newState = await _apiClient.IncrementProgressAsync();
            if (newState != null)
            {
                // Immediately update visuals (don't wait for polling)
                UpdateStreamDeckVisuals(newState);
                _lastKnownState = newState;
            }
        });
    }

    /// <summary>
    /// Handles the decrease button press.
    /// Calls the API to decrement progress.
    /// </summary>
    private void HandleDecreaseButton()
    {
        _logger.LogInformation("Decrease button pressed");
        
        _ = Task.Run(async () =>
        {
            ProgressState? newState = await _apiClient.DecrementProgressAsync();
            if (newState != null)
            {
                UpdateStreamDeckVisuals(newState);
                _lastKnownState = newState;
            }
        });
    }

    /// <summary>
    /// Clears all keys on the Stream Deck (sets them to black).
    /// Useful for cleanup when shutting down.
    /// </summary>
    public void ClearAllKeys()
    {
        if (_streamDeck == null) return;

        lock (_deviceLock)
        {
            try
            {
                _logger.LogInformation("Clearing all Stream Deck keys");
                _streamDeck.SetBrightness(100);
                
                // Clear all keys to black
                for (int i = 0; i < _streamDeck.Keys.Count; i++)
                {
                    _streamDeck.SetKeyBitmap(i, KeyBitmap.Black);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Stream Deck keys");
            }
        }
    }

    /// <summary>
    /// Converts BMP image bytes to raw RGB24 byte array.
    /// Stream Deck expects BGR24 format, so we need to convert.
    /// </summary>
    private byte[] ConvertToRgb(byte[] bmpBytes)
    {
        using (MemoryStream ms = new MemoryStream(bmpBytes))
        {
            using (Image<Rgb24> img = Image.Load<Rgb24>(ms))
            {
                // Extract raw RGB24 pixel data
                byte[] pixels = new byte[img.Width * img.Height * 3];
                img.CopyPixelDataTo(pixels);
                
                // Convert RGB to BGR
                for (int i = 0; i < pixels.Length; i += 3)
                {
                    (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                }
                
                return pixels;
            }
        }
    }

    /// <summary>
    /// Disposes resources and disconnects from Stream Deck.
    /// </summary>
    public void Dispose()
    {
        StopPolling();
        ClearAllKeys();
        
        if (_streamDeck != null)
        {
            _streamDeck.KeyStateChanged -= OnKeyStateChanged;
            _streamDeck.Dispose();
            _streamDeck = null;
        }
        
        _pollCancellation?.Dispose();
    }
}
