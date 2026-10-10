using Doorbell.Api.Authentication;
using Doorbell.Api.Demo;
using Doorbell.Api.Endpoints;
using Doorbell.Application;
using Doorbell.Infrastructure;
using Doorbell.Infrastructure.Messaging.Notifications;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "MyConnection")));

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddDoorbellAuthentication(
    builder.Configuration,
    builder.Environment);

builder.Services.AddExceptionHandler<Doorbell.Api.ExceptionHandling.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.Configure<DemoOptions>(
        builder.Configuration.GetSection("Demo"));

    builder.Services.AddHttpClient(
        "DemoMockDevice",
        client => client.Timeout = TimeSpan.FromSeconds(20));

    builder.Services.AddSingleton<DemoSimulationService>();

    builder.Services.AddHostedService<DemoMockDeviceSimulator>();
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapDemoEndpoints();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapObjectStorageEndpoints();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllers();

app.Run();
