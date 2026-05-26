using Microsoft.Extensions.Logging;
using OpenMacroBoard.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;
using StreamDeckSharp;

namespace Colosseo.StreamDeckClient.Device;

public sealed class StreamDeckDeviceConnection : IStreamDeckDeviceConnection
{
  private readonly ILogger _logger;

  public StreamDeckDeviceConnection(ILogger<StreamDeckDeviceConnection> logger)
  {
      _logger = logger;
  }

  public event EventHandler<KeyStateChangedEventArgs> KeyStateChanged;

  private IMacroBoard _device;

  public bool IsConnected => _device != null;
  public int KeyCount { get; private set; }

  public Task ConnectAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    _logger.LogInformation("Searching for Stream Deck devices...");
    _device = StreamDeck.OpenDevice();

    if (_device == null)
      throw new InvalidOperationException("No Stream Deck device found (OpenDevice returned null).");

    KeyCount = _device.Keys.Count;

    // Event z OpenMacroBoard SDK
    _device.KeyStateChanged += DeviceOnKeyStateChanged;

    _logger.LogInformation("Connected. Keys={KeyCount}", KeyCount);
    return Task.CompletedTask;
  }

  public Task SetBrightnessAsync(byte percent, CancellationToken ct)
  {
    if (_device == null) throw new InvalidOperationException("Device not connected.");
    ct.ThrowIfCancellationRequested();

    _device.SetBrightness(percent);
    return Task.CompletedTask;
  }

  public Task ClearAsync(CancellationToken ct)
  {
    if (_device == null) throw new InvalidOperationException("Device not connected.");
    ct.ThrowIfCancellationRequested();

    _device.ClearKeys();
    return Task.CompletedTask;
  }

  private void DeviceOnKeyStateChanged(object sender, KeyEventArgs e)
  {
    // KeyEventArgs typicky obsahuje Key (index) + IsDown
    KeyStateChanged?.Invoke(this, new KeyStateChangedEventArgs(e.Key, e.IsDown));
  }

  public void SetKeyBitmapBgr24(int keyIndex, int width, int height, byte[] bgr24)
  {
    if (_device == null) throw new InvalidOperationException("Device not connected.");
    if (bgr24 == null) throw new ArgumentNullException(nameof(bgr24));

    var bmp = KeyBitmap.Create.FromBgr24Array(width, height, bgr24);
    _device.SetKeyBitmap(keyIndex, bmp);
  }
  //public void SetKeyBitmap(int keyIndex, IKeyBitmap bitmap)
  //{
  //  if (_device is null) throw new InvalidOperationException("Device not connected.");
  //  _device.SetKeyBitmap(keyIndex, bitmap);
  //}

  public ValueTask DisposeAsync()
  {
    if (_device != null)
    {
      try
      {
        _device.KeyStateChanged -= DeviceOnKeyStateChanged;
        _device.ClearKeys();
      }
      catch
      {
        // ignore – při shutdownu nechceme spadnout
      }

      _device.Dispose();
      _device = null;
    }

    return ValueTask.CompletedTask;
  }
}
