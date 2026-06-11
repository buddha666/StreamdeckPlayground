using OpenMacroBoard.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Device
{
  public interface IStreamDeckDeviceConnection : IAsyncDisposable
  {
    event EventHandler<KeyStateChangedEventArgs> KeyStateChanged;

    bool IsConnected { get; }
    int KeyCount { get; }

    /// <summary>Number of key columns detected from the connected device (valid after <see cref="ConnectAsync"/>).</summary>
    int Columns { get; }

    /// <summary>Number of key rows detected from the connected device (valid after <see cref="ConnectAsync"/>).</summary>
    int Rows { get; }

    Task ConnectAsync(CancellationToken ct);
    Task SetBrightnessAsync(byte percent, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);

    void SetKeyBitmapBgr24(int keyIndex, int width, int height, byte[] bgr24);
  }

  public sealed class KeyStateChangedEventArgs : EventArgs
  {
    public KeyStateChangedEventArgs(int keyIndex, bool isDown)
    {
      KeyIndex = keyIndex;
      IsDown = isDown;
    }
    public int KeyIndex { get; }
    public bool IsDown { get; }
  }
}
