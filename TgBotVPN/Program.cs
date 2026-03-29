using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using TgBotVPN.Configuration;
using TgBotVPN.Data;
using TgBotVPN.Services;

// Setup configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Setup Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("Starting Telegram VPN Bot...");

try
{
    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureServices((context, services) =>
    {
        // Configuration
        services.AddSingleton(configuration);

        // Configure typed options
        services.Configure<TelegramBotSettings>(configuration.GetSection(TelegramBotSettings.SectionName));
        services.Configure<OutlineApiSettings>(configuration.GetSection(OutlineApiSettings.SectionName));
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<KeyUpdateServiceSettings>(configuration.GetSection(KeyUpdateServiceSettings.SectionName));

        // Database
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var dbSettings = provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            options.UseSqlite(dbSettings.ConnectionString);
        });

        // Services
        services.AddSingleton<DatabaseService>(provider =>
        {
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var logger = provider.GetRequiredService<ILogger<DatabaseService>>();
            var adminValidationService = provider.GetRequiredService<AdminValidationService>();
            return new DatabaseService(adminValidationService, logger, scopeFactory);
        });
        services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var botOpts = provider.GetRequiredService<IOptions<TelegramBotSettings>>().Value;
            var token = botOpts.Token ?? throw new InvalidOperationException("Bot token not configured");
            return new TelegramBotClient(token);
        });
        services.AddScoped<TelegramBotService>();
        services.AddScoped<AdminService>();
        services.AddScoped<UserService>();
        services.AddHostedService<KeyUpdateService>();
        services.AddScoped<OutlineApiService>();
        services.AddSingleton<AdminValidationService>();
    });

    builder.UseSerilog();
    var host = builder.Build();

    // Ensure database is created
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        Log.Information("Database initialized");
    }

    // Start the bot
    var botService = host.Services.GetRequiredService<TelegramBotService>();
    var cts = new CancellationTokenSource();

    // Handle console cancellation
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // Start background services
    _ = host.RunAsync(cts.Token);

    await botService.StartAsync(cts.Token);

    Log.Information("Bot is running. Managing outline server with {URL}",
        host.Services.GetRequiredService<IOptions<OutlineApiSettings>>().Value.Url);

    // Keep the application running
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Log.Information("Bot stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.CloseAndFlush();
}