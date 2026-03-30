using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using App.TaskSequencer.Infrastructure.Persistence;
using App.TaskSequencer.BusinessLogic.Services;
using App.TaskSequencer.Orchestration.Generators;

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
var grainClient = new ClientBuilder()
    .UseLocalhostClustering()
    .ConfigureLogging(logging => logging.AddDebug())
    .Build();

// Retry connection with exponential backoff (silo may take time to start)
int retries = 0;
const int maxRetries = 10;
const int retryDelayMs = 1000;

while (retries < maxRetries)
{
    try
    {
        await grainClient.Connect();
        Console.WriteLine("✓ Successfully connected to Orleans silo\n");
        break;
    }
    catch (Exception ex) when (retries < maxRetries - 1)
    {
        retries++;
        Console.WriteLine($"⏳ Connection attempt {retries}/{maxRetries} failed: {ex.Message}");
        Console.WriteLine($"   Retrying in {retryDelayMs}ms...");
        await Task.Delay(retryDelayMs);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Failed to connect to Orleans silo after {maxRetries} attempts: {ex.Message}");
        Console.WriteLine("  Ensure Aspire.AppHost is running: cd src/Aspire.AppHost && dotnet run");
        Environment.Exit(1);
    }
}

// Get the Orleans-based ExecutionPlanGenerator service
var generator = host.Services.GetRequiredService<OrleansExecutionPlanGenerator>();

// Define CSV file paths - use absolute path from workspace root
var workspaceRoot = @"c:\repos\vc\TaskSequencer";
var taskDefinitionPath = Path.Combine(workspaceRoot, "data", "task_definitions.csv");
var intakeEventPath = Path.Combine(workspaceRoot, "data", "intake_events.csv");
var durationHistoryPath = Path.Combine(workspaceRoot, "data", "execution_durations.csv");

try
{
    Console.WriteLine("Generating execution plan using Orleans grains...");
    Console.WriteLine("Initializing Orleans silo for iterative time slot calculation...\n");
    
    var plan = await generator.GenerateExecutionPlanAsync(
        taskDefinitionPath,
        intakeEventPath,
        durationHistoryPath);

    Console.WriteLine($"\n=== EXECUTION PLAN (ORLEANS CALCULATED) ===");
    Console.WriteLine($"Increment: {plan.IncrementId}");
    Console.WriteLine($"Valid Tasks: {plan.TotalValidTasks}");
    Console.WriteLine($"Invalid Tasks: {plan.TotalInvalidTasks}");
    Console.WriteLine($"Critical Path Completion: {plan.CriticalPathCompletion:g}");
    
    if (plan.DeadlineMisses.Count > 0)
    {
        Console.WriteLine($"\nDeadline Misses:");
        foreach (var miss in plan.DeadlineMisses)
        {
            Console.WriteLine($"  - {miss}");
        }
    }

    Console.WriteLine($"\nExecution Sequence (Dependency Order):");
    for (int i = 0; i < plan.TaskChain.Count; i++)
    {
        var tasks = plan.Tasks.Where(t => t.TaskIdString == plan.TaskChain[i]);
        foreach (var task in tasks)
        {
            Console.WriteLine($"  {i + 1}. {task.TaskIdString} ({task.TaskName})");
            Console.WriteLine($"       Scheduled: {task.ScheduledStartTime:g}");
            Console.WriteLine($"       Adjusted: {task.FunctionalStartTime?.ToString("g") ?? "No adjustment"}");
            Console.WriteLine($"       Completion: {task.PlannedCompletionTime:g}");
            if (task.RequiredEndTime.HasValue)
                Console.WriteLine($"       Deadline: {task.RequiredEndTime:g}");
        }
    }

    Console.WriteLine($"\nExecution plan generated successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    Environment.Exit(1);
}

// Exit cleanly
Environment.Exit(0);
