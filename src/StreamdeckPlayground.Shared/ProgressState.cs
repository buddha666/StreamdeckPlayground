namespace StreamdeckPlayground.Shared;

/// <summary>
/// Represents the current state of the progress bar.
/// This DTO is used to communicate progress state between the Web API and clients.
/// </summary>
/// <remarks>
/// The progress is represented in discrete steps (0 to Max).
/// For an 8-step progress bar:
/// - Step 0 = 0% (empty)
/// - Step 1 = 12.5%
/// - Step 2 = 25%
/// - ...
/// - Step 8 = 100% (full)
/// </remarks>
public class ProgressState
{
    /// <summary>
    /// The current step value (0 to Max inclusive).
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    /// The maximum step value (default is 8 for an 8-step progress bar).
    /// </summary>
    public int Max { get; set; }

    /// <summary>
    /// The progress as a percentage (0 to 100).
    /// Calculated as: (Step / Max) * 100
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// Creates a new ProgressState instance.
    /// </summary>
    public ProgressState()
    {
        Step = 0;
        Max = 8;
        Percent = 0;
    }

    /// <summary>
    /// Creates a new ProgressState instance with the specified values.
    /// </summary>
    /// <param name="step">The current step (0 to max)</param>
    /// <param name="max">The maximum number of steps</param>
    public ProgressState(int step, int max)
    {
        Step = step;
        Max = max;
        Percent = CalculatePercent(step, max);
    }

    /// <summary>
    /// Calculates the percentage based on current step and max steps.
    /// </summary>
    /// <param name="step">Current step value</param>
    /// <param name="max">Maximum step value</param>
    /// <returns>Percentage value (0-100)</returns>
    private static int CalculatePercent(int step, int max)
    {
        if (max == 0) return 0;
        return (int)Math.Round((double)step / max * 100);
    }
}
