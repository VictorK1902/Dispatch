using Azure.Messaging.ServiceBus;
using Dispatch.Api.Services;
using Dispatch.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DispatchConnection")));

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus")));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<ServiceBusClient>().CreateSender(builder.Configuration["ServiceBus:QueueName"]));

builder.Services.AddHostedService<ServiceBusCleanupService>();

builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
