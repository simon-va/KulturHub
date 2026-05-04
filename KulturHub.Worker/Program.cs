using KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;
using KulturHub.Infrastructure;
using KulturHub.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GenerateWeeklyPostHandler).Assembly));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<WeeklyPostJob>();
builder.Services.AddHostedService<TokenRefreshJob>();

var host = builder.Build();

host.Run();
