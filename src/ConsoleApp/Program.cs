using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Client.Core.Service;
using Client.Core.Contract;
using Manager.Orchestration.Service;
using Manager.Orchestration.Contract;
using Engine.Sequencing.Service;
using Engine.Sequencing.Contract;
using Access.DataModel.Service;
using Access.DataModel.Contract;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Client layer
        services.AddSingleton<IClientService, ConsoleClientService>();

        // Manager layer
        services.AddSingleton<IOrchestrationService, WorkflowOrchestrationService>();

        // Engine layer
        services.AddSingleton<ISequencingEngine, ExecutionSequencingEngine>();

        // Access layer
        services.AddSingleton<IDataAccessService, CsvDataAccessService>();
    })
    .Build();

await host.RunAsync();
