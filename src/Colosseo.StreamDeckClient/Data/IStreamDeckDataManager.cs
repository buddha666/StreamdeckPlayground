using Colosseo.Flow.Domain.DomainClasses;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Data;

public interface IStreamDeckDataManager
{
  Task<IReadOnlyList<Bank>> GetBanksAsync(CancellationToken ct);

  Task<IReadOnlyList<Tab>> GetTabsAsync(int bankId, CancellationToken ct);

  Task<IReadOnlyList<ITabEventBase>> GetTabEventsAsync(int tabId, CancellationToken ct);
  Task PlaySingleTabEventLive(int idEvent, CancellationToken ct);
  Task PlayCompositeTabEventLive(int idEvent, CancellationToken ct);
  Task<Stream> GetTabEventThumbnailAsync(int eventId, CancellationToken ct);
}
