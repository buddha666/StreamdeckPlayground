using Colosseo.Flow.Domain.DomainClasses;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Data;

/// <summary>
/// OSD / action-stop type codes sent to the Flow system.
/// NOTE: If Colosseo.Flow.Domain already defines this enum, remove this declaration
/// and add the appropriate using alias.
/// </summary>
public enum SystemEventTypeCode
{
  OsdTop,
  OsdMiddle,
  OsdBottom,
}

public interface IStreamDeckDataManager
{
  Task<IReadOnlyList<Bank>> GetBanksAsync(CancellationToken ct);

  Task<IReadOnlyList<Tab>> GetTabsAsync(int bankId, CancellationToken ct);

  Task<IReadOnlyList<ITabEventBase>> GetTabEventsAsync(int tabId, CancellationToken ct);

  /// <summary>Returns the events (Y 0-3 only) from the QuickTab of the given bank, or an empty list when no QuickTab exists.</summary>
  Task<IReadOnlyList<ITabEventBase>> GetQuickTabEventsAsync(int bankId, CancellationToken ct);

  Task PlaySingleTabEventLive(int idEvent, CancellationToken ct);
  Task PlayCompositeTabEventLive(int idEvent, CancellationToken ct);
  Task<Stream> GetTabEventThumbnailAsync(int eventId, CancellationToken ct);

  Task HideOsdAsync(SystemEventTypeCode osdType, CancellationToken ct);
  Task StopActionAsync(CancellationToken ct);
}
