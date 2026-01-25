# Stream Deck Playground

A complete learning-focused solution demonstrating how to integrate **Blazor Server** web applications with **Elgato Stream Deck XL** hardware via REST APIs.

## 🎯 Purpose

This project serves as a comprehensive example for developers learning:
- Blazor Server web development
- REST API design and implementation
- Stream Deck hardware integration
- Cross-platform image generation
- Thread-safe state management
- Configuration-driven applications

## 📋 Prerequisites

### Required
- **Windows Operating System** (Stream Deck support)
- **.NET 8.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Elgato Stream Deck XL** (or other Stream Deck model with configuration adjustments)
- **USB Connection** for Stream Deck

### Recommended
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **Postman** or **curl** for API testing (optional)

## 🏗️ Solution Structure

```
StreamdeckPlayground/
├── src/
│   ├── StreamdeckPlayground.Shared/          # Shared DTOs and models
│   │   └── ProgressState.cs                  # Progress state model
│   ├── StreamdeckPlayground.Web/             # Blazor Server web application
│   │   ├── Services/
│   │   │   └── ProgressService.cs            # Thread-safe progress state management
│   │   ├── Controllers/
│   │   │   └── ProgressController.cs         # REST API endpoints
│   │   └── Components/Pages/
│   │       └── Home.razor                    # Main UI page
│   ├── StreamdeckPlayground.StreamDeckClient/ # Stream Deck console client
│   │   ├── Services/
│   │   │   ├── ProgressApiClient.cs          # REST API client
│   │   │   ├── ImageGenerator.cs             # Key image generation
│   │   │   └── StreamDeckController.cs       # Stream Deck integration
│   │   ├── Configuration/
│   │   │   └── AppSettings.cs                # Configuration models
│   │   ├── appsettings.json                  # Configuration file
│   │   └── Program.cs                        # Application entry point
│   └── StreamdeckPlayground.StreamDeckService/ # Windows Service (skeleton)
│       ├── Worker.cs                         # Background worker
│       ├── Program.cs                        # Service entry point
│       └── README.md                         # Service documentation
├── README.md                                 # This file
├── .editorconfig                             # Code style configuration
└── StreamdeckPlayground.slnx                 # Solution file
```

## 🚀 Quick Start

> **📘 New here?** Check out [QUICKSTART.md](QUICKSTART.md) for a step-by-step guide to verify everything works!

### 1. Clone the Repository

```bash
git clone https://github.com/buddha666/StreamdeckPlayground.git
cd StreamdeckPlayground
```

### 2. Build the Solution

```bash
dotnet build
```

### 3. Run the Web Application

Open a terminal and navigate to the web project:

```bash
cd src/StreamdeckPlayground.Web
dotnet run
```

The web application will start at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

Open your browser to `http://localhost:5000` to see the progress control UI.

### 4. Run the Stream Deck Client

Open a **second terminal** (keep the web app running) and navigate to the client:

```bash
cd src/StreamdeckPlayground.StreamDeckClient
dotnet run
```

The client will:
1. Search for and connect to your Stream Deck
2. Test the API connection
3. Start polling for progress updates
4. Display progress on the top row (8 keys)
5. Show increase/decrease buttons on the second row

## 🎮 Using the Application

### Web UI
- **Increase Button**: Adds 1 step (1/8 = 12.5%) to progress
- **Decrease Button**: Subtracts 1 step from progress
- **Progress Bar**: Visual representation showing 0-100%
- **Step Counter**: Shows current step (0-8) and percentage

### Stream Deck XL
- **Top Row (Keys 0-7)**: Progress visualization
  - Green keys = filled segments
  - Dark gray keys = empty segments
  - Number of lit keys = current step count
- **Second Row Key 8**: Increase button (shows + symbol and mini progress bar)
- **Second Row Key 9**: Decrease button (shows - symbol and mini progress bar)

### REST API

Test the API using curl or Postman:

```bash
# Get current progress
curl http://localhost:5000/api/progress

# Increment progress
curl -X POST http://localhost:5000/api/progress/inc

# Decrement progress
curl -X POST http://localhost:5000/api/progress/dec

# Set progress to specific step (0-8)
curl -X PUT http://localhost:5000/api/progress/5
```

**Example Response:**
```json
{
  "step": 3,
  "max": 8,
  "percent": 38
}
```

## ⚙️ Configuration

### Stream Deck Client Configuration

Edit `src/StreamdeckPlayground.StreamDeckClient/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000",    // Change if web app runs elsewhere
    "PollIntervalMs": 250                   // Poll frequency (milliseconds)
  },
  "StreamDeck": {
    "ProgressRowStartIndex": 0,             // First key of progress row
    "IncreaseButtonIndex": 8,               // Increase button position
    "DecreaseButtonIndex": 9                // Decrease button position
  }
}
```

### Adapting for Other Stream Deck Models

#### Stream Deck (Regular - 15 keys)
```json
"StreamDeck": {
  "ProgressRowStartIndex": 0,    // Uses keys 0-7 (spans 2 rows)
  "IncreaseButtonIndex": 13,
  "DecreaseButtonIndex": 14
}
```
**Note**: Update `KeyWidth` and `KeyHeight` in `ImageGenerator.cs` to `72`.

#### Stream Deck Mini (6 keys)
The mini has only 6 keys, insufficient for an 8-step progress bar. Consider:
- Reducing max steps to 6 or fewer
- Using a different visualization approach
- Update `KeyWidth` and `KeyHeight` in `ImageGenerator.cs` to `80`.

### Key Layout Reference

**Stream Deck XL (32 keys):**
```
Row 1:  0   1   2   3   4   5   6   7
Row 2:  8   9  10  11  12  13  14  15
Row 3: 16  17  18  19  20  21  22  23
Row 4: 24  25  26  27  28  29  30  31
```

**Stream Deck (15 keys):**
```
Row 1:  0   1   2   3   4
Row 2:  5   6   7   8   9
Row 3: 10  11  12  13  14
```

**Stream Deck Mini (6 keys):**
```
Row 1:  0   1   2
Row 2:  3   4   5
```

## 🧩 Architecture Overview

### Thread-Safe State Management

The `ProgressService` uses `ReaderWriterLockSlim` for thread-safe operations:
- **Multiple concurrent readers** (API GET requests)
- **Exclusive writer access** (API POST/PUT requests)
- **Event notifications** for real-time UI updates

### REST API Design

Clean, RESTful endpoints:
- `GET /api/progress` - Retrieve state
- `POST /api/progress/inc` - Increment
- `POST /api/progress/dec` - Decrement
- `PUT /api/progress/{step}` - Set to specific step

### Stream Deck Integration

Uses `StreamDeckSharp` library:
1. **Device Discovery**: Automatic detection of connected devices
2. **Event Handling**: Key press/release events
3. **Image Rendering**: Dynamic image generation with SixLabors.ImageSharp
4. **Polling**: Background task monitors API for changes

### Image Generation

Runtime image creation using SixLabors.ImageSharp:
- **Solid color blocks** for progress segments
- **Mini progress bars** for control buttons
- **Symbols** (+ and -) for visual clarity
- **BMP format** for fast encoding

### Windows Service (Optional)

The solution includes a skeleton Windows Service project (`StreamdeckPlayground.StreamDeckService`) that demonstrates how to run the Stream Deck client as a background service. This is **disabled by default** and serves as a learning template.

**See**: `src/StreamdeckPlayground.StreamDeckService/README.md` for detailed instructions on:
- Enabling the service
- Installation and configuration
- Running as a Windows Service
- Troubleshooting service-specific issues

## 🔧 Troubleshooting

### Stream Deck Not Detected

**Symptoms**: "No Stream Deck device found" error

**Solutions**:
1. Ensure Stream Deck is connected via USB
2. Close Elgato Stream Deck software (it locks the device)
3. **Windows**: Try running as Administrator
4. **Linux**: May need udev rules for device permissions
5. Check Device Manager to verify device is recognized

### API Connection Failed

**Symptoms**: "Failed to connect to API" error

**Solutions**:
1. Verify web app is running: `http://localhost:5000`
2. Check `BaseUrl` in `appsettings.json`
3. Test API manually: `curl http://localhost:5000/api/progress`
4. Check firewall settings
5. Ensure correct port number (default: 5000)

### Image Display Issues

**Symptoms**: Black keys or incorrect images

**Solutions**:
1. Verify `KeyWidth` and `KeyHeight` match your Stream Deck model
2. Check image generation code for exceptions (see logs)
3. Ensure SixLabors.ImageSharp packages are installed
4. Try clearing all keys and restarting the client

### Build Errors

**Symptoms**: Compilation failures

**Solutions**:
1. Ensure .NET 8.0 SDK is installed: `dotnet --version`
2. Restore packages: `dotnet restore`
3. Clean and rebuild: `dotnet clean && dotnet build`
4. Check for missing NuGet packages

## 📚 Learning Resources

### Key Concepts Demonstrated

1. **Blazor Server**
   - Component-based UI
   - Server-side rendering
   - Real-time updates with SignalR

2. **REST API**
   - HTTP methods (GET, POST, PUT)
   - JSON serialization
   - Error handling

3. **Dependency Injection**
   - Service registration
   - Lifetime management (Singleton, Transient)
   - Constructor injection

4. **Configuration**
   - appsettings.json
   - Strongly-typed settings
   - Environment-specific configuration

5. **Async Programming**
   - async/await patterns
   - Task-based operations
   - CancellationTokens

6. **Hardware Integration**
   - USB device communication
   - Event-driven programming
   - Real-time updates

## 🤝 Contributing

This is a learning project. Feel free to:
- Experiment with the code
- Add new features
- Improve documentation
- Report issues

## 📄 License

This project is provided as-is for educational purposes.

## 🙏 Acknowledgments

- **StreamDeckSharp** - Elgato Stream Deck library
- **SixLabors.ImageSharp** - Cross-platform image processing
- **Microsoft** - .NET, Blazor, and ASP.NET Core

## 📞 Support

For questions or issues:
1. Check this README's troubleshooting section
2. Review the extensive code comments
3. Open an issue on GitHub

---

**Happy Learning! 🚀**
