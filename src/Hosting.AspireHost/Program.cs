using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Orleans Silo Host service
var orleansSilo = builder
    .AddExecutable(
        "orleans-silo",
        "dotnet",
        workingDirectory: "../App.TaskSequencer.OrleansHost",
        args: new[] { "run" })
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

// Add Console Client service that depends on Orleans
//TODO: DependsOn method not available in Aspire.Hosting API
//var consoleClient = builder
//    .AddExecutable(
//        "console-client",
//        "dotnet",
//        workingDirectory: "../Client.Desktop.Console",
//        args: new[] { "run" })
//    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
//    .DependsOn(orleansSilo);

var consoleClient = builder
    .AddExecutable(
        "console-client",
        "dotnet",
        workingDirectory: "../Client.Desktop.Console",
        args: new[] { "run" })
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

// Build and run
await builder.Build().RunAsync();
