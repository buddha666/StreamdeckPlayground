using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.UI.Pages;

public interface IRefreshablePage
{
  Task<bool> RefreshAsync(CancellationToken ct);
}
