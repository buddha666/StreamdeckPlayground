using Colosseo.StreamDeckClient.UI.Pages;
using System.Threading;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Rendering;

public interface IStreamDeckRenderer
{
  Task RenderAsync(ScrollableListPage page, bool canGoBack, CancellationToken ct);
}
