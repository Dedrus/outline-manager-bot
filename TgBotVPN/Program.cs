using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TgBotVPN.Configuration;
using TgBotVPN.Data;
using TgBotVPN.Services;

// Setup configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Setup Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
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
        services.AddScoped<DatabaseService>((provider) =>
        {
            var ctx = provider.GetRequiredService<AppDbContext>();
            var botSettings = provider.GetRequiredService<IOptions<TelegramBotSettings>>().Value;
            return new DatabaseService(ctx, botSettings.AdminTelegramId);
        });
        services.AddHttpClient<OutlineApiService>()
            .ConfigureHttpClient(client => { })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                return handler;
            });
        services.AddScoped<TelegramBotService>();
        services.AddHostedService<KeyUpdateService>();
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

    Log.Information("Bot is running. Press Ctrl+C to stop.");

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
