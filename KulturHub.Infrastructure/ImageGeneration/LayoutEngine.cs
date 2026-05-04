using KulturHub.Domain.Models;

namespace KulturHub.Infrastructure.ImageGeneration;

public class LayoutEngine
{
    public List<List<DayGroup>> Paginate(List<ChaynsEvent> events)
    {
        var groups = events
            .GroupBy(e => e.StartDate.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var date = DateOnly.FromDateTime(g.Key);
                return new DayGroup
                {
                    Date    = date,
                    DayName = DateFormatHelper.FormatDayHeader(date),
                    Events  = g.OrderBy(e => e.StartDate.TimeOfDay).ToList(),
                };
            })
            .ToList();

        var pages   = new List<List<DayGroup>>();
        var current = new List<DayGroup>();
        int used    = 0;

        // Reserve MoreElementReservedHeight on every page because at fill-time
        // we do not yet know whether this will be the last page.
        int available = LayoutConstants.ContentAreaHeight - LayoutConstants.MoreElementReservedHeight;

        foreach (var day in groups)
        {
            int dayHeight = CalculateDayHeight(day.Events.Count);

            if (current.Count > 0 && used + dayHeight > available)
            {
                pages.Add(current);
                current = [];
                used    = 0;
            }

            current.Add(day);
            used += dayHeight;
        }

        if (current.Count > 0)
            pages.Add(current);

        return pages;
    }

    public int CalculateDayHeight(int eventCount)
        => LayoutConstants.DayHeaderTopPadding
         + LayoutConstants.DayHeaderHeight
         + eventCount * (LayoutConstants.EventTitleRowHeight
                       + LayoutConstants.EventLocationRowHeight
                       + LayoutConstants.EventSpacing);
}
