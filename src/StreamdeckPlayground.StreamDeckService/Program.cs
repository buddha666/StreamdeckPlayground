using StreamdeckPlayground.StreamDeckService;

/// <summary>
/// Windows Service wrapper for the Stream Deck Client.
/// 
/// This project demonstrates how to run the Stream Deck client as a Windows Service.
/// By default, this is DISABLED and serves as a skeleton/example.
/// 
/// To enable and use this as a Windows Service:
/// 
/// 1. Build the project in Release mode:
///    dotnet publish -c Release -o C:\StreamDeckService
/// 
/// 2. Install the service using sc.exe (run as Administrator):
///    sc create StreamDeckService binPath=C:\StreamDeckService\StreamdeckPlayground.StreamDeckService.exe
/// 
/// 3. Start the service:
///    sc start StreamDeckService
/// 
/// 4. Stop the service:
///    sc stop StreamDeckService
/// 
/// 5. Delete the service:
///    sc delete StreamDeckService
/// 
/// Note: To make this functional, you would need to:
/// - Copy the Stream Deck client code into this project's Worker
/// - Add references to StreamDeckSharp and other dependencies
/// - Handle service lifecycle events properly
/// - Configure the service to run under an account with USB device access
/// </summary>

var builder = Host.CreateApplicationBuilder(args);

// Enable Windows Service support
// This allows the app to run as a Windows Service OR as a console app
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Stream Deck Progress Control Service";
});

// Register the background worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
