using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ServiceDiscovery;
using Orleans;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/orleans-silo-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = new HostBuilder()
        .UseOrleans(siloBuilder =>
        {
            siloBuilder
                .UseLocalhostClustering()
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(Program).Assembly).WithReferences();
                });
        })
        .ConfigureServices(services =>
        {
            services.AddServiceDiscovery();
            services.AddHealthChecks();
        })
        .UseSerilog()
        .Build();

    Log.Information("Orleans Silo Host starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orleans Silo Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
