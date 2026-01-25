namespace StreamdeckPlayground.StreamDeckService;

/// <summary>
/// Background worker for the Windows Service.
/// 
/// This is a SKELETON implementation that demonstrates the structure.
/// To make this functional, you would need to:
/// 
/// 1. Add project reference to StreamdeckPlayground.StreamDeckClient
///    OR copy the client code into this project
/// 
/// 2. Replace this worker's implementation with the Stream Deck client logic:
///    - Initialize StreamDeckController
///    - Start API polling
///    - Handle key events
/// 
/// 3. Add proper startup/shutdown handling:
///    - Connect to Stream Deck in ExecuteAsync
///    - Gracefully disconnect in StopAsync
/// 
/// Example structure:
/// 
/// private StreamDeckController? _controller;
/// 
/// protected override async Task ExecuteAsync(CancellationToken stoppingToken)
/// {
///     _logger.LogInformation("Stream Deck Service starting...");
///     
///     // Initialize controller
///     _controller = serviceProvider.GetRequiredService<StreamDeckController>();
///     
///     if (_controller.Connect())
///     {
///         _controller.StartPolling(250);
///         _logger.LogInformation("Stream Deck Service running");
///         
///         // Keep running until cancellation
///         await Task.Delay(Timeout.Infinite, stoppingToken);
///     }
/// }
/// 
/// public override async Task StopAsync(CancellationToken cancellationToken)
/// {
///     _logger.LogInformation("Stream Deck Service stopping...");
///     _controller?.Dispose();
///     await base.StopAsync(cancellationToken);
/// }
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Main execution loop for the background service.
    /// This runs when the Windows Service starts.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stream Deck Service (Skeleton) started");
        _logger.LogInformation("This is a skeleton implementation - see comments for how to enable");

        // Skeleton implementation - just log periodically to show it's running
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Service heartbeat at: {time}", DateTimeOffset.Now);
            
            // In a real implementation, this would be:
            // - Stream Deck connection and monitoring
            // - API polling
            // - Key event handling
            
            await Task.Delay(10000, stoppingToken); // Log every 10 seconds
        }
        
        _logger.LogInformation("Stream Deck Service stopping");
    }

    /// <summary>
    /// Called when the service is stopping.
    /// Use this to cleanup resources (disconnect from Stream Deck, etc.)
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stream Deck Service shutdown requested");
        
        // Cleanup would go here:
        // - Stop polling
        // - Clear Stream Deck keys
        // - Disconnect from device
        
        await base.StopAsync(cancellationToken);
    }
}
