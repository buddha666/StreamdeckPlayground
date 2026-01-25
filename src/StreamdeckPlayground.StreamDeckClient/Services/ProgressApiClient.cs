using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StreamdeckPlayground.Shared;

namespace StreamdeckPlayground.StreamDeckClient.Services;

/// <summary>
/// Client service for communicating with the Progress REST API.
/// Handles all HTTP requests to get and modify progress state.
/// </summary>
/// <remarks>
/// This service encapsulates all REST API communication, making it easy to:
/// 1. Monitor progress state via polling
/// 2. Increment/decrement progress from Stream Deck buttons
/// 3. Handle network errors gracefully
/// 
/// Thread Safety: HttpClient is thread-safe for concurrent requests.
/// </remarks>
public class ProgressApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProgressApiClient> _logger;

    public ProgressApiClient(HttpClient httpClient, ILogger<ProgressApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current progress state from the API.
    /// Endpoint: GET /api/progress
    /// </summary>
    /// <returns>Current progress state, or null if the request fails</returns>
    public async Task<ProgressState?> GetProgressAsync()
    {
        try
        {
            // Send GET request to /api/progress
            // JsonSerializer will automatically deserialize the response to ProgressState
            ProgressState? state = await _httpClient.GetFromJsonAsync<ProgressState>("api/progress");
            
            if (state != null)
            {
                _logger.LogDebug("Retrieved progress: step={Step}, percent={Percent}%", 
                    state.Step, state.Percent);
            }
            
            return state;
        }
        catch (HttpRequestException ex)
        {
            // Network errors (connection refused, DNS failure, etc.)
            _logger.LogError(ex, "Network error getting progress from API");
            return null;
        }
        catch (Exception ex)
        {
            // Other errors (JSON deserialization, etc.)
            _logger.LogError(ex, "Unexpected error getting progress from API");
            return null;
        }
    }

    /// <summary>
    /// Increments the progress by one step via the API.
    /// Endpoint: POST /api/progress/inc
    /// </summary>
    /// <returns>New progress state, or null if the request fails</returns>
    public async Task<ProgressState?> IncrementProgressAsync()
    {
        try
        {
            _logger.LogInformation("Sending increment request to API");
            
            // Send POST request to /api/progress/inc
            // The API returns the new state as JSON
            HttpResponseMessage response = await _httpClient.PostAsync("api/progress/inc", null);
            response.EnsureSuccessStatusCode();
            
            ProgressState? newState = await response.Content.ReadFromJsonAsync<ProgressState>();
            
            if (newState != null)
            {
                _logger.LogInformation("Progress incremented to step={Step} ({Percent}%)", 
                    newState.Step, newState.Percent);
            }
            
            return newState;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error incrementing progress");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error incrementing progress");
            return null;
        }
    }

    /// <summary>
    /// Decrements the progress by one step via the API.
    /// Endpoint: POST /api/progress/dec
    /// </summary>
    /// <returns>New progress state, or null if the request fails</returns>
    public async Task<ProgressState?> DecrementProgressAsync()
    {
        try
        {
            _logger.LogInformation("Sending decrement request to API");
            
            // Send POST request to /api/progress/dec
            HttpResponseMessage response = await _httpClient.PostAsync("api/progress/dec", null);
            response.EnsureSuccessStatusCode();
            
            ProgressState? newState = await response.Content.ReadFromJsonAsync<ProgressState>();
            
            if (newState != null)
            {
                _logger.LogInformation("Progress decremented to step={Step} ({Percent}%)", 
                    newState.Step, newState.Percent);
            }
            
            return newState;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error decrementing progress");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error decrementing progress");
            return null;
        }
    }

    /// <summary>
    /// Sets the progress to a specific step value via the API.
    /// Endpoint: PUT /api/progress/{step}
    /// </summary>
    /// <param name="step">Step value to set (0 to 8)</param>
    /// <returns>New progress state, or null if the request fails</returns>
    public async Task<ProgressState?> SetProgressAsync(int step)
    {
        try
        {
            _logger.LogInformation("Setting progress to step={Step}", step);
            
            // Send PUT request to /api/progress/{step}
            HttpResponseMessage response = await _httpClient.PutAsync($"api/progress/{step}", null);
            response.EnsureSuccessStatusCode();
            
            ProgressState? newState = await response.Content.ReadFromJsonAsync<ProgressState>();
            
            if (newState != null)
            {
                _logger.LogInformation("Progress set to step={Step} ({Percent}%)", 
                    newState.Step, newState.Percent);
            }
            
            return newState;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error setting progress to step {Step}", step);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting progress to step {Step}", step);
            return null;
        }
    }
}
