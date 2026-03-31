using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;

try
{
    var host = new HostBuilder()
        .UseOrleans(siloBuilder =>
        {
            siloBuilder
                .UseLocalhostClustering()
                //.ConfigureApplicationParts(parts =>
                //{
                //    parts.AddApplicationPart(typeof(Program).Assembly).WithReferences();
                //})
                ;
        })
        .ConfigureServices(services =>
        {
            services.AddHealthChecks();
        })
       .Build();

    Console.WriteLine("Orleans Silo Host starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Orleans Silo Host terminated unexpectedly: {ex.Message}");
}
