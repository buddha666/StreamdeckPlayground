using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamdeckPlayground.StreamDeckClient.Configuration;
using StreamdeckPlayground.StreamDeckClient.Services;

namespace StreamdeckPlayground.StreamDeckClient;

/// <summary>
/// Stream Deck Client Application
/// 
/// This console application connects to an Elgato Stream Deck XL and provides
/// visual progress feedback and control buttons.
/// 
/// Features:
/// - Displays an 8-step progress bar on the top row of Stream Deck keys
/// - Provides increase/decrease buttons on the second row
/// - Polls the web API for progress updates
/// - Sends increment/decrement commands when buttons are pressed
/// 
/// Configuration:
/// - Edit appsettings.json to change API URL, polling interval, or key mapping
/// 
/// Requirements:
/// - Stream Deck XL (or other model with appropriate key configuration)
/// - Web API running (default: http://localhost:5000)
/// - USB connection to Stream Deck
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║      Stream Deck Progress Control - Client               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Build configuration from appsettings.json
            // This allows users to customize settings without recompiling
            IConfiguration configuration;
            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n❌ Failed to load configuration file.");
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine("\nTroubleshooting:");
                Console.WriteLine("  • Ensure appsettings.json exists in the same directory as the executable");
                Console.WriteLine($"  • Current directory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine("  • Try running 'dotnet build' to ensure the file is copied");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return 1;
            }

            // Setup dependency injection container
            // This is good practice and makes the code testable
            ServiceCollection services = new ServiceCollection();
            
            // Configure logging
            // Logs go to console with timestamps and log levels
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // Bind configuration sections to strongly-typed settings classes
            ApiSettings apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>() 
                ?? new ApiSettings();
            StreamDeckSettings streamDeckSettings = configuration.GetSection("StreamDeck").Get<StreamDeckSettings>() 
                ?? new StreamDeckSettings();

            // Register settings as singletons so they can be injected
            services.AddSingleton(apiSettings);
            services.AddSingleton(streamDeckSettings);

            // Register HttpClient for API communication
            // BaseAddress is set from configuration
            services.AddHttpClient<ProgressApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiSettings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            // Register our services
            services.AddSingleton<ImageGenerator>();
            services.AddSingleton<StreamDeckController>();

            // Build the service provider (DI container)
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            // Get logger for main program
            ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting Stream Deck Client");
            logger.LogInformation("API Base URL: {BaseUrl}", apiSettings.BaseUrl);
            logger.LogInformation("Poll Interval: {Interval}ms", apiSettings.PollIntervalMs);
            logger.LogInformation("Key Mapping - Progress Row Start: {Start}", streamDeckSettings.ProgressRowStartIndex);
            logger.LogInformation("Key Mapping - Increase Button: {Index}", streamDeckSettings.IncreaseButtonIndex);
            logger.LogInformation("Key Mapping - Decrease Button: {Index}", streamDeckSettings.DecreaseButtonIndex);

            // Get the Stream Deck controller
            StreamDeckController controller = serviceProvider.GetRequiredService<StreamDeckController>();

            try
            {
                // Connect to Stream Deck
                Console.WriteLine("\n[1/3] Connecting to Stream Deck...");
                if (!controller.Connect())
                {
                    Console.WriteLine("\n❌ Failed to connect to Stream Deck.");
                    Console.WriteLine("\nTroubleshooting:");
                    Console.WriteLine("  • Ensure Stream Deck is connected via USB");
                    Console.WriteLine("  • Close Elgato Stream Deck software if running");
                    Console.WriteLine("  • Try running as Administrator (Windows) or with sudo (Linux)");
                    Console.WriteLine("  • Check Device Manager / System Info for device status");
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey();
                    return 1;
                }
                Console.WriteLine("✅ Connected to Stream Deck");

                // Test API connection
                Console.WriteLine("\n[2/3] Testing API connection...");
                ProgressApiClient apiClient = serviceProvider.GetRequiredService<ProgressApiClient>();
                var initialState = await apiClient.GetProgressAsync();
                
                if (initialState == null)
                {
                    Console.WriteLine("\n❌ Failed to connect to API at {0}", apiSettings.BaseUrl);
                    Console.WriteLine("\nTroubleshooting:");
                    Console.WriteLine("  • Ensure the web application is running");
                    Console.WriteLine("  • Check the BaseUrl in appsettings.json");
                    Console.WriteLine("  • Try accessing {0}/api/progress in a browser", apiSettings.BaseUrl);
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey();
                    return 1;
                }
                Console.WriteLine("✅ Connected to API - Current progress: {0}/{1} ({2}%)", 
                    initialState.Step, initialState.Max, initialState.Percent);

                // Start polling
                Console.WriteLine("\n[3/3] Starting polling loop...");
                controller.StartPolling(apiSettings.PollIntervalMs);
                Console.WriteLine("✅ Polling started");

                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  Stream Deck Client is running!                           ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
                Console.WriteLine("║  • Top row shows progress (8 keys light up)               ║");
                Console.WriteLine("║  • Second row has increase/decrease buttons               ║");
                Console.WriteLine("║  • Changes from web UI or API appear on Stream Deck       ║");
                Console.WriteLine("║  • Press buttons to control progress                      ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
                Console.WriteLine("║  Press CTRL+C or close window to exit                     ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
                Console.WriteLine();

                // Setup graceful shutdown
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true; // Prevent immediate termination
                    logger.LogInformation("Shutdown requested");
                    cts.Cancel();
                };

                // Wait for cancellation (CTRL+C)
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when CTRL+C is pressed
                logger.LogInformation("Shutting down gracefully...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in main loop");
                Console.WriteLine("\n❌ An error occurred: {0}", ex.Message);
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return 1;
            }
            finally
            {
                // Cleanup
                Console.WriteLine("\nCleaning up...");
                controller.Dispose();
                serviceProvider.Dispose();
                Console.WriteLine("✅ Shutdown complete");
            }

            return 0;
        }
        catch (Exception ex)
        {
            // Catch any unhandled exceptions at the top level
            Console.WriteLine("\n❌ Fatal error during startup:");
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"\nDetails: {ex}");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return 1;
        }
    }
}
