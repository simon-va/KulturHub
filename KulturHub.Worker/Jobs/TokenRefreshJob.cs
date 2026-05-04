using KulturHub.Application.Features.Instagram.RefreshToken;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KulturHub.Worker.Jobs;

public class TokenRefreshJob(
    IServiceScopeFactory scopeFactory,
    ILogger<TokenRefreshJob> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly TimeZoneInfo CentralEurope =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly TimeOnly TriggerTime = new(3, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runImmediately = configuration.GetValue<bool>("Worker:RunImmediately");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (runImmediately)
            {
                logger.LogInformation("TokenRefreshJob running immediately (debug mode).");
            }
            else
            {
                var delay = GetDelayUntilNextDailyTrigger();
                logger.LogInformation("TokenRefreshJob waiting {Delay:g} until next trigger (03:00 CET).", delay);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;
            }

            await RunJobAsync(stoppingToken);

            if (runImmediately)
                break;
        }
    }

    private async Task RunJobAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var result = await mediator.Send(new RefreshInstagramTokenCommand(), cancellationToken);

            if (result.IsError)
                logger.LogError("TokenRefreshJob failed: {Error}", result.FirstError.Description);
            else
                logger.LogInformation("TokenRefreshJob completed, success: {Success}.", result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TokenRefreshJob failed unexpectedly.");
        }
    }

    private static TimeSpan GetDelayUntilNextDailyTrigger()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CentralEurope);
        var nextTrigger = now.Date.Add(TriggerTime.ToTimeSpan());

        if (nextTrigger <= now.DateTime)
            nextTrigger = nextTrigger.AddDays(1);

        return nextTrigger - now.DateTime;
    }
}
