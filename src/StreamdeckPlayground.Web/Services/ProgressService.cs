using StreamdeckPlayground.Shared;

namespace StreamdeckPlayground.Web.Services;

/// <summary>
/// Thread-safe service for managing progress state.
/// This service is registered as a singleton and shared between the UI and API endpoints.
/// </summary>
/// <remarks>
/// Thread Safety:
/// - Uses a ReaderWriterLockSlim for efficient concurrent access
/// - Multiple readers can access state simultaneously
/// - Writers get exclusive access during modifications
/// - This is important because both the Blazor UI and REST API access this service
/// 
/// Why Singleton?
/// - We want ONE source of truth for the progress state
/// - All clients (web UI, REST API, Stream Deck) see the same value
/// - State changes are immediately visible to all consumers
/// </remarks>
public class ProgressService : IDisposable
{
    // Thread-safe lock for protecting the _currentState field
    // ReaderWriterLockSlim allows multiple concurrent readers but exclusive writers
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    
    // The actual progress state
    private ProgressState _currentState;
    
    // Logger for tracking state changes
    private readonly ILogger<ProgressService> _logger;

    /// <summary>
    /// Maximum number of steps in the progress bar (default: 8 for Stream Deck XL top row)
    /// </summary>
    public const int MaxSteps = 8;

    /// <summary>
    /// Event fired when the progress state changes.
    /// Useful for real-time UI updates via SignalR or similar mechanisms.
    /// </summary>
    public event EventHandler<ProgressState>? ProgressChanged;

    public ProgressService(ILogger<ProgressService> logger)
    {
        _logger = logger;
        _currentState = new ProgressState(0, MaxSteps);
        _logger.LogInformation("ProgressService initialized with MaxSteps={MaxSteps}", MaxSteps);
    }

    /// <summary>
    /// Gets the current progress state.
    /// Thread-safe: Multiple threads can read simultaneously.
    /// </summary>
    /// <returns>A copy of the current progress state</returns>
    public ProgressState GetState()
    {
        _lock.EnterReadLock();
        try
        {
            // Return a copy to prevent external modification of internal state
            return new ProgressState(_currentState.Step, _currentState.Max);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Increments the progress by one step (clamped to MaxSteps).
    /// Thread-safe: Uses write lock for exclusive access.
    /// </summary>
    /// <returns>The new progress state after incrementing</returns>
    public ProgressState Increment()
    {
        _lock.EnterWriteLock();
        try
        {
            int oldStep = _currentState.Step;
            
            // Clamp to maximum value
            if (_currentState.Step < MaxSteps)
            {
                _currentState.Step++;
            }

            _currentState.Percent = CalculatePercent(_currentState.Step, MaxSteps);

            _logger.LogInformation("Progress incremented: {OldStep} -> {NewStep} ({Percent}%)", 
                oldStep, _currentState.Step, _currentState.Percent);

            // Create a copy to return and fire event
            ProgressState newState = new ProgressState(_currentState.Step, _currentState.Max);
            OnProgressChanged(newState);
            return newState;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Decrements the progress by one step (clamped to 0).
    /// Thread-safe: Uses write lock for exclusive access.
    /// </summary>
    /// <returns>The new progress state after decrementing</returns>
    public ProgressState Decrement()
    {
        _lock.EnterWriteLock();
        try
        {
            int oldStep = _currentState.Step;
            
            // Clamp to minimum value
            if (_currentState.Step > 0)
            {
                _currentState.Step--;
            }

            _currentState.Percent = CalculatePercent(_currentState.Step, MaxSteps);

            _logger.LogInformation("Progress decremented: {OldStep} -> {NewStep} ({Percent}%)", 
                oldStep, _currentState.Step, _currentState.Percent);

            // Create a copy to return and fire event
            ProgressState newState = new ProgressState(_currentState.Step, _currentState.Max);
            OnProgressChanged(newState);
            return newState;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Sets the progress to a specific step value (clamped to 0..MaxSteps).
    /// Thread-safe: Uses write lock for exclusive access.
    /// </summary>
    /// <param name="step">The step value to set (0 to MaxSteps)</param>
    /// <returns>The new progress state after setting</returns>
    public ProgressState SetStep(int step)
    {
        _lock.EnterWriteLock();
        try
        {
            int oldStep = _currentState.Step;
            
            // Clamp to valid range
            _currentState.Step = Math.Clamp(step, 0, MaxSteps);
            _currentState.Percent = CalculatePercent(_currentState.Step, MaxSteps);

            _logger.LogInformation("Progress set directly: {OldStep} -> {NewStep} ({Percent}%)", 
                oldStep, _currentState.Step, _currentState.Percent);

            // Create a copy to return and fire event
            ProgressState newState = new ProgressState(_currentState.Step, _currentState.Max);
            OnProgressChanged(newState);
            return newState;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Calculates the percentage based on current step and max steps.
    /// </summary>
    private static int CalculatePercent(int step, int max)
    {
        if (max == 0) return 0;
        return (int)Math.Round((double)step / max * 100);
    }

    /// <summary>
    /// Raises the ProgressChanged event.
    /// </summary>
    protected virtual void OnProgressChanged(ProgressState newState)
    {
        ProgressChanged?.Invoke(this, newState);
    }

    /// <summary>
    /// Disposes the lock resources.
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}
