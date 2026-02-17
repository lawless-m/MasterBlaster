namespace MasterBlaster;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using MasterBlaster.Config;
using MasterBlaster.Claude;
using MasterBlaster.Execution;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;
using MasterBlaster.Tcp;
using Microsoft.Extensions.Logging;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configOption = new Option<string>(
            "--config",
            getDefaultValue: () => "./config.json",
            description: "Path to configuration file");

        var rootCommand = new RootCommand("MasterBlaster — automate legacy Windows applications via RDP and Claude AI");
        rootCommand.AddGlobalOption(configOption);

        // masterblaster run <task> --param1 value1 --param2 value2
        var runCommand = new Command("run", "Execute a single task and exit");
        var taskArgument = new Argument<string>("task", "Task name (filename without .mbl extension)");
        runCommand.AddArgument(taskArgument);

        // Remaining arguments are treated as --param_name value pairs
        runCommand.TreatUnmatchedTokensAsErrors = false;

        runCommand.SetHandler(async (InvocationContext context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            var taskName = context.ParseResult.GetValueForArgument(taskArgument);
            var unmatchedTokens = context.ParseResult.UnmatchedTokens;

            var parameters = ParseParameters(unmatchedTokens);
            var exitCode = await RunTaskAsync(configPath, taskName, parameters);
            context.ExitCode = exitCode;
        });

        // masterblaster validate <task>
        var validateCommand = new Command("validate", "Parse and validate an MBL task file without executing");
        var validateTaskArgument = new Argument<string>("task", "Task name to validate");
        validateCommand.AddArgument(validateTaskArgument);

        validateCommand.SetHandler(async (InvocationContext context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            var taskName = context.ParseResult.GetValueForArgument(validateTaskArgument);
            context.ExitCode = await ValidateTaskAsync(configPath, taskName);
        });

        // masterblaster list
        var listCommand = new Command("list", "List available tasks");
        listCommand.SetHandler(async (InvocationContext context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            context.ExitCode = await ListTasksAsync(configPath);
        });

        // masterblaster service
        var serviceCommand = new Command("service", "Start in service mode (TCP listener)");
        serviceCommand.SetHandler(async (InvocationContext context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            context.ExitCode = await RunServiceAsync(configPath, context.GetCancellationToken());
        });

        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(validateCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(serviceCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Dictionary<string, string> ParseParameters(IReadOnlyList<string> tokens)
    {
        var parameters = new Dictionary<string, string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("--") && i + 1 < tokens.Count)
            {
                var key = token[2..]; // Remove --
                var value = tokens[i + 1];
                parameters[key] = value;
                i++; // Skip value
            }
        }
        return parameters;
    }

    private static async Task<int> RunTaskAsync(string configPath, string taskName, Dictionary<string, string> parameters)
    {
        try
        {
            var config = ConfigLoader.Load(configPath);
            using var loggerFactory = CreateLoggerFactory(config);

            var taskLogger = new TaskLogger(config.Logging, loggerFactory.CreateLogger<TaskLogger>());
            var screenshotManager = new ScreenshotManager(config.Logging);

            // Load and parse task
            var taskFilePath = Path.Combine(config.Tasks.Directory, $"{taskName}.mbl");
            if (!File.Exists(taskFilePath))
            {
                Console.Error.WriteLine($"Task file not found: {taskFilePath}");
                return 1;
            }

            var mblSource = await File.ReadAllTextAsync(taskFilePath);
            var lexer = new Lexer();
            var tokens = lexer.Tokenize(mblSource);
            var parser = new Parser();
            var taskDefinition = parser.Parse(tokens, taskName);

            var validator = new Validator();
            var errors = validator.Validate(taskDefinition);
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Task validation errors:");
                foreach (var error in errors)
                    Console.Error.WriteLine($"  - {error}");
                return 1;
            }

            // Create components
            var rdp = new MstscRdpController(loggerFactory.CreateLogger<MstscRdpController>());
            var rdpConfig = new RdpConnectionConfig
            {
                Server = config.Rdp.Server,
                Port = config.Rdp.Port,
                Username = config.Rdp.Username,
                Password = Environment.GetEnvironmentVariable(config.Rdp.PasswordEnv) ?? "",
                Domain = config.Rdp.Domain,
                Width = config.Rdp.Width,
                Height = config.Rdp.Height,
                ColorDepth = config.Rdp.ColorDepth
            };

            await rdp.ConnectAsync(rdpConfig);

            var claude = new ClaudeClient(config.Claude, config.Rdp.Width, config.Rdp.Height,
                loggerFactory.CreateLogger<ClaudeClient>());

            var executor = new TaskExecutor(rdp, claude, config.Tasks, taskLogger,
                loggerFactory.CreateLogger<TaskExecutor>());

            var result = await executor.ExecuteAsync(taskDefinition, parameters);

            // Output result as JSON
            var json = JsonSerializer.Serialize(new
            {
                status = result.Success ? "success" : "error",
                task = taskName,
                outputs = result.Success ? result.Outputs : null,
                error = result.Error,
                failed_at_step = result.FailedAtStep,
                screenshot = result.ScreenshotPath,
                duration_ms = result.DurationMs,
                steps_completed = result.StepsCompleted,
                steps_total = result.StepsTotal,
                log_file = result.LogFile
            }, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

            Console.WriteLine(json);

            await rdp.DisconnectAsync();
            await rdp.DisposeAsync();

            return result.Success ? 0 : 1;
        }
        catch (MblParseException ex)
        {
            Console.Error.WriteLine($"Parse error at line {ex.Line}: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Engine error: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> ValidateTaskAsync(string configPath, string taskName)
    {
        try
        {
            var config = ConfigLoader.Load(configPath);
            var taskFilePath = Path.Combine(config.Tasks.Directory, $"{taskName}.mbl");

            if (!File.Exists(taskFilePath))
            {
                Console.Error.WriteLine($"Task file not found: {taskFilePath}");
                return 1;
            }

            var mblSource = await File.ReadAllTextAsync(taskFilePath);
            var lexer = new Lexer();
            var tokens = lexer.Tokenize(mblSource);
            var parser = new Parser();
            var taskDefinition = parser.Parse(tokens, taskName);

            var validator = new Validator();
            var errors = validator.Validate(taskDefinition);

            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Validation errors:");
                foreach (var error in errors)
                    Console.Error.WriteLine($"  - {error}");
                return 1;
            }

            Console.WriteLine($"Task '{taskName}' is valid.");
            Console.WriteLine($"  Name: {taskDefinition.Name}");
            Console.WriteLine($"  Inputs: {string.Join(", ", taskDefinition.Inputs)}");
            Console.WriteLine($"  Steps: {taskDefinition.Steps.Count}");
            Console.WriteLine($"  Has on_timeout handler: {taskDefinition.OnTimeout != null}");
            Console.WriteLine($"  Has on_error handler: {taskDefinition.OnError != null}");

            return 0;
        }
        catch (MblParseException ex)
        {
            Console.Error.WriteLine($"Parse error at line {ex.Line}: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static Task<int> ListTasksAsync(string configPath)
    {
        try
        {
            var config = ConfigLoader.Load(configPath);
            var tasksDir = config.Tasks.Directory;

            if (!Directory.Exists(tasksDir))
            {
                Console.Error.WriteLine($"Tasks directory not found: {tasksDir}");
                return Task.FromResult(1);
            }

            var tasks = Directory.GetFiles(tasksDir, "*.mbl")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();

            if (tasks.Count == 0)
            {
                Console.WriteLine("No tasks found.");
            }
            else
            {
                Console.WriteLine("Available tasks:");
                foreach (var task in tasks)
                    Console.WriteLine($"  {task}");
            }

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return Task.FromResult(2);
        }
    }

    private static async Task<int> RunServiceAsync(string configPath, CancellationToken ct)
    {
        try
        {
            var config = ConfigLoader.Load(configPath);
            using var loggerFactory = CreateLoggerFactory(config);
            var logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("MasterBlaster service starting...");

            // Create components
            var rdp = new MstscRdpController(loggerFactory.CreateLogger<MstscRdpController>());
            var rdpConfig = new RdpConnectionConfig
            {
                Server = config.Rdp.Server,
                Port = config.Rdp.Port,
                Username = config.Rdp.Username,
                Password = Environment.GetEnvironmentVariable(config.Rdp.PasswordEnv) ?? "",
                Domain = config.Rdp.Domain,
                Width = config.Rdp.Width,
                Height = config.Rdp.Height,
                ColorDepth = config.Rdp.ColorDepth
            };

            await rdp.ConnectAsync(rdpConfig, ct);
            logger.LogInformation("RDP connected to {Server}", config.Rdp.Server);

            var claude = new ClaudeClient(config.Claude, config.Rdp.Width, config.Rdp.Height,
                loggerFactory.CreateLogger<ClaudeClient>());

            var taskLogger = new TaskLogger(config.Logging, loggerFactory.CreateLogger<TaskLogger>());
            var executor = new TaskExecutor(rdp, claude, config.Tasks, taskLogger,
                loggerFactory.CreateLogger<TaskExecutor>());

            var requestHandler = new RequestHandler(
                executor, rdp, config, taskLogger,
                loggerFactory.CreateLogger<RequestHandler>());

            var server = new TcpServer(config.Tcp, requestHandler,
                loggerFactory.CreateLogger<TcpServer>());

            logger.LogInformation("Starting TCP listener on {Host}:{Port}", config.Tcp.Host, config.Tcp.Port);
            await server.StartAsync(ct);

            logger.LogInformation("MasterBlaster service stopped.");

            await rdp.DisconnectAsync();
            await rdp.DisposeAsync();

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Service error: {ex.Message}");
            return 2;
        }
    }

    private static ILoggerFactory CreateLoggerFactory(AppConfig config)
    {
        var logLevel = config.Logging.LogLevel.ToLowerInvariant() switch
        {
            "debug" or "trace" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information
        };

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            builder.AddConsole();
        });
    }
}
