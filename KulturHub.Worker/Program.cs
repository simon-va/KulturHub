using KulturHub.Application;
using KulturHub.Infrastructure;
using KulturHub.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<WeeklyPostJob>();
builder.Services.AddHostedService<TokenRefreshJob>();

var host = builder.Build();

host.Run();
