using KulturHub.Application.Features.WeeklyPost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KulturHub.Worker.Jobs;

public class WeeklyPostJob(
    IServiceScopeFactory scopeFactory,
    ILogger<WeeklyPostJob> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly TimeZoneInfo CentralEurope =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runImmediately = configuration.GetValue<bool>("Worker:RunImmediately");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (runImmediately)
            {
                logger.LogInformation("WeeklyPostJob running immediately (debug mode).");
            }
            else
            {
                var delay = GetDelayUntilNextSunday();
                logger.LogInformation("WeeklyPostJob waiting {Delay:g} until next trigger (Sunday 18:00 CET).", delay);

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
        var weeklyPostService = scope.ServiceProvider.GetRequiredService<IWeeklyPostService>();

        try
        {
            var result = await weeklyPostService.GenerateWeeklyPostAsync(cancellationToken);

            if (result.IsError)
                logger.LogError("WeeklyPostJob failed: {Error}", result.FirstError.Description);
            else
                logger.LogInformation("Weekly post generated: {PostId}.", result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WeeklyPostJob failed unexpectedly.");
        }
    }

    private static TimeSpan GetDelayUntilNextSunday()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CentralEurope);
        var triggerTime = new TimeOnly(18, 0);

        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;

        var nextTrigger = now.Date.AddDays(daysUntilSunday).Add(triggerTime.ToTimeSpan());

        if (nextTrigger <= now.DateTime)
            nextTrigger = nextTrigger.AddDays(7);

        return nextTrigger - now.DateTime;
    }
}
