# Stream Deck Windows Service (Skeleton)

This project demonstrates how to run the Stream Deck client as a Windows Service.

**⚠️ IMPORTANT: This is a SKELETON/EXAMPLE implementation and is DISABLED by default.**

## Purpose

This project shows the structure for running the Stream Deck client as a background Windows Service that:
- Starts automatically when Windows boots
- Runs without a user being logged in
- Doesn't require a console window

## Current Status

This is a **skeleton implementation** that serves as a learning template. It includes:
- ✅ Windows Service hosting setup
- ✅ Background worker structure
- ✅ Lifecycle management (start/stop)
- ✅ Logging configuration
- ❌ Stream Deck client integration (not implemented)

## How to Enable This Project

To make this functional, you would need to:

### 1. Add Dependencies

Either:
- **Option A**: Add project reference to `StreamdeckPlayground.StreamDeckClient`
- **Option B**: Copy the Stream Deck client code into this project

### 2. Update Worker.cs

Replace the skeleton `Worker` implementation with actual Stream Deck client logic:

```csharp
private StreamDeckController? _controller;
private readonly IServiceProvider _serviceProvider;

public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
{
    _logger = logger;
    _serviceProvider = serviceProvider;
}

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("Stream Deck Service starting...");
    
    // Get controller from DI
    _controller = _serviceProvider.GetRequiredService<StreamDeckController>();
    
    if (_controller.Connect())
    {
        _controller.StartPolling(250);
        _logger.LogInformation("Stream Deck connected and polling");
        
        // Keep running until service is stopped
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    else
    {
        _logger.LogError("Failed to connect to Stream Deck");
    }
}

public override async Task StopAsync(CancellationToken cancellationToken)
{
    _logger.LogInformation("Stream Deck Service stopping...");
    _controller?.Dispose();
    await base.StopAsync(cancellationToken);
}
```

### 3. Register Services in Program.cs

Add the Stream Deck services to dependency injection:

```csharp
// Add configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Register settings
builder.Services.AddSingleton(configuration.GetSection("ApiSettings").Get<ApiSettings>());
builder.Services.AddSingleton(configuration.GetSection("StreamDeck").Get<StreamDeckSettings>());

// Register HttpClient
builder.Services.AddHttpClient<ProgressApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});

// Register Stream Deck services
builder.Services.AddSingleton<ImageGenerator>();
builder.Services.AddSingleton<StreamDeckController>();
```

## Installing as a Windows Service

Once functional, install using these steps:

### 1. Publish the Application

```powershell
dotnet publish -c Release -o C:\StreamDeckService
```

### 2. Create the Service (Run as Administrator)

```powershell
sc create StreamDeckService binPath="C:\StreamDeckService\StreamdeckPlayground.StreamDeckService.exe" start=auto
```

Parameters:
- `StreamDeckService` - Service name
- `binPath` - Full path to the executable
- `start=auto` - Start automatically with Windows

### 3. Configure Service Account (Important!)

The service must run under an account with USB device access:

```powershell
# Option 1: Run as Local System (has device access)
sc config StreamDeckService obj=LocalSystem

# Option 2: Run as specific user (replace with your username)
sc config StreamDeckService obj=".\YourUsername" password="YourPassword"
```

### 4. Start the Service

```powershell
sc start StreamDeckService
```

### 5. Check Service Status

```powershell
sc query StreamDeckService
```

## Managing the Service

### View Logs

Logs are written to Windows Event Log:
1. Open Event Viewer
2. Navigate to: Windows Logs > Application
3. Look for source: `Stream Deck Progress Control Service`

### Stop the Service

```powershell
sc stop StreamDeckService
```

### Remove the Service

```powershell
sc delete StreamDeckService
```

## Troubleshooting

### Service Won't Start

**Possible causes:**
1. USB device access issues - try running as LocalSystem
2. Dependencies not found - ensure all DLLs are in the publish folder
3. Configuration file missing - copy appsettings.json to publish folder

### Stream Deck Not Detected

**Solutions:**
1. Ensure Elgato Stream Deck software is closed
2. Configure service to run as LocalSystem or user with device access
3. Check Windows Device Manager for device status

### Service Starts But Doesn't Work

**Check:**
1. Event Viewer logs for errors
2. Web API is running and accessible
3. Configuration file (appsettings.json) is correct

## Alternative: Task Scheduler

For simpler deployments, consider using Windows Task Scheduler instead:

1. Create a new task
2. Set trigger: "At system startup"
3. Set action: Start the console application
4. Set "Run whether user is logged on or not"

This avoids service complexity while providing auto-start functionality.

## Why Use a Service vs Console App?

**Advantages:**
- Starts automatically with Windows
- Runs without user login
- Better for production/unattended scenarios
- Managed by Windows Service Control Manager

**Disadvantages:**
- More complex to debug
- Requires administrator privileges to install
- No console output (must use logging)
- USB device permissions can be tricky

## Further Reading

- [.NET Worker Services](https://docs.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Windows Services](https://docs.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [sc.exe Command Reference](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create)
