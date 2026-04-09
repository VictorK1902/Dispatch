using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dispatch.Data;
using Dispatch.Worker.Interfaces;
using Dispatch.Worker.Services;
using Dispatch.Worker.Services.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScottPlot;

// Bundle a font to avoid missing system fonts on Linux containers
var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans.ttf");
Fonts.AddFontFile("DejaVu Sans", fontPath);
Fonts.Default = "DejaVu Sans";

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

/*
The ConfigureFunctionsApplicationInsights() call silently registers a filter rule that sets the minimum log level to Warning on the ApplicationInsightsLoggerProvider.
It's hardcoded inside that extension method.
So even though host.json says "accept Information", the worker's own App Insights logger says "I only care about Warning and above" and drops LogInformation calls before they leave the worker process.
The removal code finds that specific hardcoded rule and deletes it, so the worker defers to host.json for log level decisions — which is the expectation.
It's a known design issue with the isolated worker model. The host and worker have independent logging pipelines.
*/
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    var defaultRule = options.Rules.FirstOrDefault(rule =>
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (defaultRule is not null)
    {
        options.Rules.Remove(defaultRule);
    }
});

// Azure Communication Services - Email
var acsEndpoint = builder.Configuration["AcsEndpoint"] ?? throw new InvalidOperationException("AcsEndpoint is not configured");

builder.Services.AddDbContext<DispatchDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DispatchConnection")));

builder.Services
        .AddSingleton(new EmailClient(new Uri(acsEndpoint), new DefaultAzureCredential()))
        .Configure<EmailServiceOptions>(options =>
        {
            options.SenderAddress = builder.Configuration["AcsSenderAddress"] ?? throw new InvalidOperationException("AcsSenderAddress is not configured");
            options.AdminEmail = builder.Configuration["AdminEmail"] ?? throw new InvalidOperationException("AdminEmail is not configured");
        })
        .AddSingleton<IEmailService, EmailService>();

builder.Services.AddHttpClient<IWeatherApiService, WeatherApiService>(client =>
            client.BaseAddress = new Uri(builder.Configuration["WeatherApiUrl"] ?? throw new InvalidOperationException("WeatherApiUrl is not configured")));

builder.Services.Configure<StockPriceApiServiceOptions>(options =>
            options.ApiKey = builder.Configuration["AlphaVantageApiKey"] ?? throw new InvalidOperationException("AlphaVantageApiKey is not configured"))
        .AddHttpClient<IStockPriceApiService, StockPriceApiService>(client =>
            client.BaseAddress = new Uri(builder.Configuration["AlphaVantageApiUrl"] ?? throw new InvalidOperationException("AlphaVantageApiUrl is not configured")));

builder.Services
        .AddScoped<IJobModuleHandler, WeatherReportHandler>()
        .AddScoped<IJobModuleHandler, StockPriceReportHandler>()
        .AddSingleton<IChartService, ChartService>();

builder.Build().Run();
