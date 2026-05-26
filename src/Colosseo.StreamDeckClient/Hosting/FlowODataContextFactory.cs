using Colosseo.Flow.ODataClient.Context;
using Colosseo.OData.Client.Core.Context;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Colosseo.StreamDeckClient.Hosting
{
  public class ServiceProviderFlowODataContextFactory : IFlowODataContextFactory
  {
    private readonly IServiceProvider serviceProvider;

    public ServiceProviderFlowODataContextFactory(IServiceProvider serviceProvider)
    {
      this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IFlowContext Create()
    {
      return serviceProvider.GetRequiredService<FlowContext>();
    }

    public IFlowContext CreateFlow()
    {
      return serviceProvider.GetRequiredService<FlowContext>();
    }

    ODataServiceContext IODataServiceContextFactory<ODataServiceContext>.Create()
    {
      throw new NotImplementedException();
    }
  }
}
