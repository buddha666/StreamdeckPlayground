# Quick Start Guide

This guide will help you verify that the Stream Deck Playground solution is working correctly.

## Prerequisites Check

Before starting, ensure you have:
- [ ] .NET 8.0 SDK installed (`dotnet --version`)
- [ ] Stream Deck XL connected via USB (or other model)
- [ ] Elgato Stream Deck software closed (to free the device)

## Step 1: Build the Solution

```bash
# Navigate to the repository root
cd StreamdeckPlayground

# Build everything
dotnet build
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see errors, check that .NET 8.0 SDK is installed.

## Step 2: Start the Web Application

Open a terminal window and run:

```bash
cd src/StreamdeckPlayground.Web
dotnet run
```

**Expected Output:**
```
Now listening on: http://localhost:5000
Now listening on: https://localhost:5001
Application started. Press Ctrl+C to shut down.
```

**Keep this terminal open** - the web app needs to stay running.

## Step 3: Test the Web UI

1. Open your web browser
2. Navigate to: `http://localhost:5000`
3. You should see the "Stream Deck Progress Control" page
4. Try clicking the **Increase** and **Decrease** buttons
5. Watch the progress bar update

**Expected behavior:**
- Progress bar starts at 0%
- Increase button adds 12.5% (1/8) per click
- Decrease button subtracts 12.5% per click
- Step counter shows current step (0-8)
- Buttons disable when at min/max

## Step 4: Test the REST API

Open a **second terminal** window (keep the web app running) and test the API:

```bash
# Get current progress
curl http://localhost:5000/api/progress

# Expected output: {"step":0,"max":8,"percent":0}

# Increment progress
curl -X POST http://localhost:5000/api/progress/inc

# Expected output: {"step":1,"max":8,"percent":13}

# Decrement progress
curl -X POST http://localhost:5000/api/progress/dec

# Expected output: {"step":0,"max":8,"percent":0}

# Set to specific step
curl -X PUT http://localhost:5000/api/progress/5

# Expected output: {"step":5,"max":8,"percent":63}
```

**Watch the web UI** - it should update automatically when you call the API!

## Step 5: Test the Stream Deck Client

Open a **third terminal** window (keep the web app running):

```bash
cd src/StreamdeckPlayground.StreamDeckClient
dotnet run
```

**Expected Output (if Stream Deck is connected):**
```
╔═══════════════════════════════════════════════════════════╗
║      Stream Deck Progress Control - Client               ║
╚═══════════════════════════════════════════════════════════╝

[1/3] Connecting to Stream Deck...
✅ Connected to Stream Deck

[2/3] Testing API connection...
✅ Connected to API - Current progress: 0/8 (0%)

[3/3] Starting polling loop...
✅ Polling started

╔═══════════════════════════════════════════════════════════╗
║  Stream Deck Client is running!                           ║
╠═══════════════════════════════════════════════════════════╣
║  • Top row shows progress (8 keys light up)               ║
║  • Second row has increase/decrease buttons               ║
║  • Changes from web UI or API appear on Stream Deck       ║
║  • Press buttons to control progress                      ║
╠═══════════════════════════════════════════════════════════╣
║  Press CTRL+C or close window to exit                     ║
╚═══════════════════════════════════════════════════════════╝
```

**If Stream Deck is NOT connected:**
```
❌ Failed to connect to Stream Deck.

Troubleshooting:
  • Ensure Stream Deck is connected via USB
  • Close Elgato Stream Deck software if running
  • Try running as Administrator (Windows) or with sudo (Linux)
  • Check Device Manager / System Info for device status
```

## Step 6: Verify Stream Deck Integration

If the Stream Deck client connected successfully:

1. **Look at your Stream Deck**:
   - Top row (8 keys): Should show progress visualization
   - Key 8 (second row, first key): Increase button with + symbol
   - Key 9 (second row, second key): Decrease button with - symbol

2. **Press the increase button** (key 8) on Stream Deck:
   - Top row should light up one more key (green)
   - Mini progress bar on button should update
   - Console should log the action

3. **Go back to the web UI** in your browser:
   - Progress bar should have updated automatically!
   - This demonstrates the full loop: Stream Deck → API → Web UI

4. **Click Increase in the web UI**:
   - Stream Deck should update within 250ms
   - Shows: Web UI → API → Stream Deck polling

## What You've Verified

If all steps worked, you've confirmed:

✅ Solution builds successfully  
✅ Web application runs and serves UI  
✅ REST API endpoints work  
✅ Real-time UI updates via events  
✅ Stream Deck client connects to device  
✅ Stream Deck displays progress visualization  
✅ Button presses on Stream Deck call the API  
✅ Full synchronization loop works  

## Common Issues

### Console Window Flashes and Closes Immediately

**Problem:** StreamDeckClient console opens and closes immediately

**Causes & Solutions:**

1. **Missing configuration file:**
   ```bash
   # Rebuild the project to ensure appsettings.json is copied
   cd src/StreamdeckPlayground.StreamDeckClient
   dotnet build
   dotnet run
   ```

2. **Running from wrong directory:**
   - Make sure you're in the `src/StreamdeckPlayground.StreamDeckClient` directory
   - Or use: `dotnet run --project src/StreamdeckPlayground.StreamDeckClient/StreamdeckPlayground.StreamDeckClient.csproj`

3. **Running the DLL directly:**
   - Don't run the .dll file directly from File Explorer
   - Use `dotnet run` from the command line instead

4. **See the error message:**
   - Run from command prompt/terminal (not by double-clicking)
   - The console will stay open and show the error
   - Error messages now wait for you to press a key

### Port Already in Use

**Problem:** Web app says "Failed to bind to address"

**Solution:**
```bash
# Use a different port
dotnet run --urls "http://localhost:5001"

# Then update appsettings.json in Stream Deck client:
# "BaseUrl": "http://localhost:5001"
```

### Stream Deck Not Detected

**Problem:** Client can't find Stream Deck

**Solutions:**
1. Close Elgato Stream Deck software
2. Unplug and replug the USB cable
3. Run as Administrator (Windows)
4. Check Device Manager for device status

### API Connection Failed

**Problem:** Stream Deck client can't reach API

**Solutions:**
1. Verify web app is running
2. Check firewall isn't blocking port 5000
3. Test API manually: `curl http://localhost:5000/api/progress`

## Next Steps

Now that everything works, you can:

1. **Explore the code** - read the extensive comments
2. **Modify the configuration** - try different key mappings
3. **Study the architecture** - understand thread safety and state management
4. **Experiment** - add new features or change the UI

## Cleanup

When you're done:

1. Press **CTRL+C** in the Stream Deck client terminal
2. Press **CTRL+C** in the web app terminal
3. The Stream Deck keys should clear automatically

## Learning Resources

- Main README: `README.md`
- Windows Service guide: `src/StreamdeckPlayground.StreamDeckService/README.md`
- Code comments: Extensive inline documentation throughout

---

**Congratulations!** You've successfully set up and verified the Stream Deck Playground! 🎉
