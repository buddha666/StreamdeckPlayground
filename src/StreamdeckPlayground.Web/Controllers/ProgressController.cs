using Microsoft.AspNetCore.Mvc;
using StreamdeckPlayground.Shared;
using StreamdeckPlayground.Web.Services;

namespace StreamdeckPlayground.Web.Controllers;

/// <summary>
/// REST API controller for managing progress state.
/// Provides endpoints for getting and modifying the progress value.
/// </summary>
/// <remarks>
/// These endpoints are designed to be called by:
/// 1. The Stream Deck client to monitor and control progress
/// 2. Any external client (curl, Postman, custom apps)
/// 
/// All endpoints return the current progress state as JSON.
/// The state is managed by the singleton ProgressService.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class ProgressController : ControllerBase
{
    private readonly ProgressService _progressService;
    private readonly ILogger<ProgressController> _logger;

    public ProgressController(ProgressService progressService, ILogger<ProgressController> logger)
    {
        _progressService = progressService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/progress
    /// Returns the current progress state.
    /// </summary>
    /// <returns>
    /// JSON object with:
    /// - step: Current step (0 to 8)
    /// - max: Maximum steps (8)
    /// - percent: Percentage (0 to 100)
    /// </returns>
    /// <example>
    /// Response: { "step": 3, "max": 8, "percent": 38 }
    /// </example>
    [HttpGet]
    public ActionResult<ProgressState> GetProgress()
    {
        ProgressState state = _progressService.GetState();
        _logger.LogInformation("GET /api/progress - Current state: step={Step}, percent={Percent}%", 
            state.Step, state.Percent);
        return Ok(state);
    }

    /// <summary>
    /// POST /api/progress/inc
    /// Increments the progress by one step (1/8).
    /// </summary>
    /// <returns>The new progress state after incrementing</returns>
    /// <example>
    /// Before: { "step": 2, "max": 8, "percent": 25 }
    /// After:  { "step": 3, "max": 8, "percent": 38 }
    /// </example>
    [HttpPost("inc")]
    public ActionResult<ProgressState> Increment()
    {
        ProgressState newState = _progressService.Increment();
        _logger.LogInformation("POST /api/progress/inc - New state: step={Step}, percent={Percent}%", 
            newState.Step, newState.Percent);
        return Ok(newState);
    }

    /// <summary>
    /// POST /api/progress/dec
    /// Decrements the progress by one step (1/8).
    /// </summary>
    /// <returns>The new progress state after decrementing</returns>
    /// <example>
    /// Before: { "step": 3, "max": 8, "percent": 38 }
    /// After:  { "step": 2, "max": 8, "percent": 25 }
    /// </example>
    [HttpPost("dec")]
    public ActionResult<ProgressState> Decrement()
    {
        ProgressState newState = _progressService.Decrement();
        _logger.LogInformation("POST /api/progress/dec - New state: step={Step}, percent={Percent}%", 
            newState.Step, newState.Percent);
        return Ok(newState);
    }

    /// <summary>
    /// PUT /api/progress/{step}
    /// Sets the progress to a specific step value.
    /// </summary>
    /// <param name="step">The step value to set (0 to 8)</param>
    /// <returns>The new progress state after setting</returns>
    /// <example>
    /// PUT /api/progress/5
    /// Response: { "step": 5, "max": 8, "percent": 63 }
    /// </example>
    [HttpPut("{step}")]
    public ActionResult<ProgressState> SetProgress(int step)
    {
        // Validate step range
        if (step < 0 || step > ProgressService.MaxSteps)
        {
            _logger.LogWarning("PUT /api/progress/{Step} - Invalid step value. Must be 0-{Max}", 
                step, ProgressService.MaxSteps);
            return BadRequest(new { 
                error = "Invalid step value", 
                message = $"Step must be between 0 and {ProgressService.MaxSteps}" 
            });
        }

        ProgressState newState = _progressService.SetStep(step);
        _logger.LogInformation("PUT /api/progress/{Step} - New state: step={Step}, percent={Percent}%", 
            step, newState.Step, newState.Percent);
        return Ok(newState);
    }
}
