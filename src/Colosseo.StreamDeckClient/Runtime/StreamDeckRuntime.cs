using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Device;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.UI.Navigation;
using Colosseo.StreamDeckClient.UI.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Colosseo.StreamDeckClient.Runtime;

public sealed class StreamDeckRuntime
{
  private readonly IStreamDeckDeviceConnection _device;
  private readonly IStreamDeckDataManager _dataManager;
  private readonly ISelectedTabProvider _selectedTab;
  private readonly INavigationService _nav;
  private readonly IStreamDeckRenderer _renderer;
  private readonly ILogger<StreamDeckRuntime> _logger;
  private readonly IOptions<StreamDeckOptions> _cfg;

  private readonly ConcurrentQueue<KeyStateChangedEventArgs> _inputQueue = new();

  private DateTimeOffset _lastAliveLog = DateTimeOffset.MinValue;
  private DateTimeOffset _nextPollAt = DateTimeOffset.MinValue;

  private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
  private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

  public StreamDeckRuntime(
    IStreamDeckDeviceConnection device,
    IStreamDeckDataManager dataManager,
    ISelectedTabProvider selectedTab,
    INavigationService navigationService,
    IStreamDeckRenderer renderer,
    IOptions<StreamDeckOptions> cfg,
    ILogger<StreamDeckRuntime> logger)
  {
    _device = device;
    _dataManager = dataManager;
    _selectedTab = selectedTab;
    _nav = navigationService;
    _renderer = renderer;
    _cfg = cfg;
    _logger = logger;
  }

  private async Task PollAndUpdateAsync(CancellationToken ct)
  {
    if (_nav.Current is IRefreshablePage refreshable)
      await refreshable.RefreshAsync(ct);
  }

  public async Task RunAsync(CancellationToken ct)
  {
    try
    {
      await _device.ConnectAsync(ct);
      _device.KeyStateChanged += OnKeyStateChanged;

      await _device.ClearAsync(ct);
      await _device.SetBrightnessAsync(50, ct);

      if (_cfg.Value.Mode == StreamDeckMode.Following)
        _nav.SetRoot(new FollowingEventsPage(_dataManager, _selectedTab, _cfg.Value));
      else
        _nav.SetRoot(new BanksPage(_dataManager, _nav, _cfg));

      await RefreshCurrentPageIfNeededAsync(force: true, ct);

      while (!ct.IsCancellationRequested)
      {
        await DrainInputAsync(ct);

        await RefreshCurrentPageIfNeededAsync(force: false, ct);

        await _renderer.RenderAsync(_nav.Current, _nav.CanGoBack, ct);

        LogAliveSometimes();

        await Task.Delay(TickInterval, ct);
      }
    }
    catch (OperationCanceledException)
    {
      // expected on shutdown
    }
    finally
    {
      try
      {
        _device.KeyStateChanged -= OnKeyStateChanged;
      }
      catch { }

      try
      {
        // IMPORTANT: clear even when ct is canceled
        await _device.ClearAsync(CancellationToken.None);
      }
      catch { }

      try
      {
        await _device.DisposeAsync();
      }
      catch { }
    }
  }

  private void OnKeyStateChanged(object sender, KeyStateChangedEventArgs e)
  {
    if (!e.IsDown) return;
    _inputQueue.Enqueue(e);
  }

  private async Task DrainInputAsync(CancellationToken ct)
  {
    while (_inputQueue.TryDequeue(out var e))
    {
      await _nav.Current.OnKeyDownAsync(e.KeyIndex, ct);
    }
  }

  private async Task RefreshCurrentPageIfNeededAsync(bool force, CancellationToken ct)
  {
    var now = DateTimeOffset.UtcNow;
    if (!force && now < _nextPollAt)
      return;

    _nextPollAt = now + PollInterval;

    if (_nav.Current is IRefreshablePage refreshable)
    {
      try
      {
        var changed = await refreshable.RefreshAsync(ct);
        if (changed)
        {
          _logger.LogDebug("Page refreshed and changed: {PageType}", _nav.Current.GetType().Name);
        }
      }
      catch (Exception ex) when (!ct.IsCancellationRequested)
      {
        _logger.LogError(ex, "Refresh failed for page {PageType}", _nav.Current.GetType().Name);
      }
    }
  }

  private void LogAliveSometimes()
  {
    var now = DateTimeOffset.UtcNow;
    if (now - _lastAliveLog > TimeSpan.FromSeconds(5))
    {
      _lastAliveLog = now;
      _logger.LogInformation("Runtime alive. Connected={Connected}, Keys={Keys}, Page={Page}",
          _device.IsConnected,
          _device.KeyCount,
          _nav.Current.GetType().Name);
    }
  }
}
