using Colosseo.Domain.Common.Models;
using Colosseo.Flow.Domain.DomainClasses;
using Colosseo.Flow.ODataClient.Providers;
using Colosseo.StreamDeckClient.Config;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Monogram.Sport.FlowBLL.Enums;

namespace Colosseo.StreamDeckClient.Data;

public sealed class StreamDeckDataManager : IStreamDeckDataManager
{
  private readonly IFlowDataProvider _flowDataProvider;
  private readonly ILogger<StreamDeckDataManager> _logger;
  private readonly IFlowControlProvider _flowControlProvider;
  private readonly StreamDeckClientConfig _cfg;

  public StreamDeckDataManager(IFlowDataProvider flow, IFlowControlProvider flowControlProvider, StreamDeckClientConfig cfg, ILogger<StreamDeckDataManager> logger)
  {
    _flowDataProvider = flow;
    _flowControlProvider = flowControlProvider;
    _cfg = cfg;
    _logger = logger;
  }

  public async Task<IReadOnlyList<Bank>> GetBanksAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    var banks = await _flowDataProvider.GetBanksAsync();

    return banks
        .OrderBy(b => b.OrderNumber)
        .ThenBy(b => b.Name)
        .ToList();
  }

  public async Task<Stream> GetTabEventThumbnailAsync(int eventId, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    return await _flowDataProvider.GetTabEventThumbnailAsync(eventId);
  }

  public async Task<IReadOnlyList<Tab>> GetTabsAsync(int bankId, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    var tabs = await _flowDataProvider.GetTabsAsync(t => t.IdBank == bankId);
    return tabs
        .OrderBy(t => t.OrderNumber)
        .ThenBy(t => t.Name)
        .ToList();
  }

  public async Task<IReadOnlyList<ITabEventBase>> GetTabEventsAsync(int tabId, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    var events = await _flowDataProvider.GetTabEventsAsync(tabId);

    return events.ToList();
  }

  public async Task<IReadOnlyList<ITabEventBase>> GetQuickTabEventsAsync(int bankId, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    var tabs = await _flowDataProvider.GetTabsAsync(t => t.IdBank == bankId);
    var quickTab = tabs.FirstOrDefault(t => t.IsQuickTab);
    if (quickTab == null)
      return System.Array.Empty<ITabEventBase>();

    var events = await _flowDataProvider.GetTabEventsAsync(quickTab.Id);
    return events
        .Where(e => e.PositionY >= 0 && e.PositionY <= 3)
        .ToList();
  }

  public async Task PlaySingleTabEventLive(int idEvent, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    if (_cfg.IsInLiveMode)
    {
      await _flowControlProvider.GoLiveAsync<TabEvent>(idEvent, _cfg.FlowSenderIdentifier);
    }
    else
    {
      await _flowControlProvider.EnqueueAsync<TabEvent>(idEvent, _cfg.FlowSenderIdentifier);
    }
  }

  public async Task PlayCompositeTabEventLive(int idEvent, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    if (_cfg.IsInLiveMode)
    {
      await _flowControlProvider.GoLiveAsync<TabEventComposition>(idEvent, _cfg.FlowSenderIdentifier);
    }
    else
    {
      await _flowControlProvider.EnqueueAsync<TabEventComposition>(idEvent, _cfg.FlowSenderIdentifier);
    }
  }

  public async Task StopAction(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    await _flowControlProvider.StopActiveEventAsync(EventTypeCode.OneTimeEvent, null);
  }

  public async Task HideOsd(SystemEventTypeCode systemEventTypeCode, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    await _flowControlProvider.StopActiveEventAsync(EventTypeCode.System, systemEventTypeCode);
  }

}
