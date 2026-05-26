using Colosseo.StreamDeckClient.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Hosting
{
  public sealed class StreamDeckWorker : BackgroundService
  {
    private readonly ILogger _logger;
    private readonly StreamDeckRuntime _runtime;

    public StreamDeckWorker(StreamDeckRuntime runtime, ILogger<StreamDeckWorker> logger)
    {
      _logger = logger;
      _runtime = runtime;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("StreamDeck worker starting...");
      await _runtime.RunAsync(stoppingToken);
      _logger.LogInformation("StreamDeck worker stopped.");
    }
  }
}
