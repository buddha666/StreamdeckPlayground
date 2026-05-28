// See https://aka.ms/new-console-template for more information
using Colosseo.Flow.ODataClient.Context;
using Colosseo.Flow.ODataClient.Providers;
using Colosseo.OData.Client.Core.Providers;
using Colosseo.StreamDeckClient.Config;
using Colosseo.StreamDeckClient.Data;
using Colosseo.StreamDeckClient.Device;
using Colosseo.StreamDeckClient.Hosting;
using Colosseo.StreamDeckClient.Rendering;
using Colosseo.StreamDeckClient.Runtime;
using Colosseo.StreamDeckClient.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
      logging.ClearProviders();
      logging.AddConsole();
      logging.AddDebug();
    })
    .ConfigureServices(services =>
    {
      // StreamDeck client configuration
      services.AddSingleton(new StreamDeckClientConfig
      {
        FlowSenderIdentifier = "showManager StreamDeck",
        IsInLiveMode = false,
        StreamDeckModeIsFollowing = false,
        StreamDeckShowStopButtons = true,
        StreamDeckShowQuickTab = true,
      });

      services.AddSingleton<IODataServiceModelProvider, ODataServiceModelProvider>();
      services.AddTransient<FlowContext>(sp =>
      {
        var serviceRoot = new Uri("http://service.colosseo.app:45289/"); 
        var modelProvider = sp.GetRequiredService<IODataServiceModelProvider>();
        return new FlowContext(serviceRoot, modelProvider);
      });

      services.AddSingleton<IStreamDeckDeviceConnection, StreamDeckDeviceConnection>();
      services.AddSingleton<IStreamDeckDataManager, StreamDeckDataManager>();
      services.AddSingleton<INavigationService, NavigationService>();
      services.AddSingleton<StreamDeckRuntime>();
      services.AddSingleton<IStreamDeckRenderer, ImageSharpStreamDeckRenderer>();
      services.AddSingleton<IFlowODataContextFactory, ServiceProviderFlowODataContextFactory>();
      //services.AddTransient<IShowManagerDataProvider, ShowManagerDataProvider>();
      services.AddTransient<IFlowDataProvider, FlowDataProvider>();
      services.AddTransient<IFlowControlProvider, FlowControlProvider>();

      services.AddHostedService<StreamDeckWorker>();
    })
    .Build();

    await host.RunAsync();
