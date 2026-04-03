using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

builder.Build().Run();




