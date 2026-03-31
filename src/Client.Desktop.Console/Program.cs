using Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Orleans;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register core services
        services.AddSingleton<ManifestCsvParser>();
        services.AddSingleton<ManifestTransformer>();
        services.AddSingleton<ExecutionEventMatrixBuilder>();
        services.AddSingleton<DependencyResolver>();
        services.AddSingleton<DeadlineValidator>();
        services.AddSingleton<ExecutionPlanGenerator>();
        services.AddSingleton<OrleansExecutionPlanGenerator>();
    })
    .Build();

// Initialize Orleans client connection to silo started by Aspire
Console.WriteLine("Connecting to Orleans silo (started by Aspire.AppHost)...");
//TODO: Fix Orleans ClientBuilder initialization - requires proper setup
//var grainClient = new ClientBuilder(host.Services as IServiceCollection ?? new ServiceCollection(), host.Services.GetRequiredService<IConfiguration>())
//    .UseLocalhostClustering()
//    .Build();

//// Retry connection with exponential backoff (silo may take time to start)
//int retries = 0;
//const int maxRetries = 10;
//const int retryDelayMs = 1000;

//while (retries < maxRetries)
//{
//    try
//    {
//        await grainClient.Connect();
//        Console.WriteLine("✓ Successfully connected to Orleans silo\n");
//        break;
//    }
//    catch (Exception ex) when (retries < maxRetries - 1)
//    {
//        retries++;
//        Console.WriteLine($"⏳ Connection attempt {retries}/{maxRetries} failed: {ex.Message}");
//        Console.WriteLine($"   Retrying in {retryDelayMs}ms...");
//        await Task.Delay(retryDelayMs);
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"✗ Failed to connect to Orleans silo after {maxRetries} attempts: {ex.Message}");
//        Console.WriteLine("  Ensure Aspire.AppHost is running: cd src/Aspire.AppHost && dotnet run");
//        Environment.Exit(1);
//    }
//}

// Get the Orleans-based ExecutionPlanGenerator service
//var generator = host.Services.GetRequiredService<OrleansExecutionPlanGenerator>();

// Define CSV file paths - use absolute path from workspace root
//var workspaceRoot = @"c:\repos\vc\TaskSequencer";
//var taskDefinitionPath = Path.Combine(workspaceRoot, "data", "task_definitions.csv");
//var intakeEventPath = Path.Combine(workspaceRoot, "data", "intake_events.csv");
//var durationHistoryPath = Path.Combine(workspaceRoot, "data", "execution_durations.csv");

//try
//{
//    Console.WriteLine("Generating execution plan using Orleans grains...");
//    Console.WriteLine("Initializing Orleans silo for iterative time slot calculation...\n");
//    
//    var plan = await generator.GenerateExecutionPlanAsync(
//        taskDefinitionPath,
//        intakeEventPath,
//        durationHistoryPath);

//    Console.WriteLine($"\n=== EXECUTION PLAN (ORLEANS CALCULATED) ===");
//    Console.WriteLine($"Increment: {plan.IncrementId}");
//    Console.WriteLine($"Valid Tasks: {plan.TotalValidTasks}");
//    Console.WriteLine($"Invalid Tasks: {plan.TotalInvalidTasks}");
//    Console.WriteLine($"Critical Path Completion: {plan.CriticalPathCompletion:g}");
    
//    if (plan.Tasks != null && plan.Tasks.Count > 0)
//    {
//        Console.WriteLine($"Total Tasks in Plan: {plan.Tasks.Count}");
//        Console.WriteLine("\nFirst 5 tasks:");
//        foreach (var task in plan.Tasks.Take(5))
//        {
//            Console.WriteLine($"  {task.TaskIdString}: {task.TaskName} (Start: {task.FunctionalStartTime:g}, Duration: {task.DurationMinutes}min)");
//        }
//        Console.WriteLine("  ...");
//    }

//    Console.WriteLine("\n✓ Execution plan generated successfully!");
//}
//catch (Exception ex)
//{
//    Console.WriteLine($"✗ Error generating execution plan: {ex.Message}");
//    Console.WriteLine(ex.StackTrace);
//    Environment.Exit(1);
//}

Console.WriteLine("Console client stub - Orleans integration not yet implemented");

// Exit cleanly
Environment.Exit(0);
