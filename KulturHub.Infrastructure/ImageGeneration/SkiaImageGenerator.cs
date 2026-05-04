using KulturHub.Application.Ports;
using KulturHub.Domain.Models;
using SkiaSharp;

namespace KulturHub.Infrastructure.ImageGeneration;

public class SkiaImageGenerator(LayoutEngine layoutEngine) : IImageGenerator
{
    private readonly LayoutEngine _layoutEngine = layoutEngine;

    private static readonly SKColor HeaderBg  = SKColor.Parse("#4eaf51");
    private static readonly SKColor IconColor = SKColor.Parse("#4eaf51");
    private static readonly SKColor DarkText  = SKColor.Parse("#1A1A1A");
    private static readonly SKColor GrayText  = SKColor.Parse("#888888");
    private static readonly SKColor White     = SKColors.White;

    public List<byte[]> GenerateWeeklyImages(List<ChaynsEvent> events, DateTime weekStart, DateTime weekEnd)
    {
        var pages   = _layoutEngine.Paginate(events);
        var results = new List<byte[]>(pages.Count);
        int total   = pages.Count;

        for (int i = 0; i < total; i++)
            results.Add(RenderPage(pages[i], pageNumber: i + 1, totalPages: total, weekStart, weekEnd));

        return results;
    }

    private static byte[] RenderPage(
        List<DayGroup> page, int pageNumber, int totalPages,
        DateTime startDate, DateTime endDate)
    {
        using var bitmap = new SKBitmap(LayoutConstants.ImageWidth, LayoutConstants.ImageHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(White);

        DrawHeader(canvas, startDate, endDate);

        float currentY = LayoutConstants.HeaderHeight;
        foreach (var day in page)
            currentY = DrawDayGroup(canvas, day, currentY);

        DrawFooter(canvas, isLastPage: pageNumber == totalPages);

        using var image   = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static void DrawHeader(SKCanvas canvas, DateTime startDate, DateTime endDate)
    {
        using var bgPaint = new SKPaint { Color = HeaderBg, IsAntialias = true };
        canvas.DrawRect(0, 0, LayoutConstants.ImageWidth, LayoutConstants.HeaderHeight, bgPaint);

        using var titleFont  = MakeFont(70, bold: true);
        using var titlePaint = MakeTextPaint(White);
        using var subFont    = MakeFont(34);
        using var subPaint   = MakeTextPaint(White);

        float x = LayoutConstants.MarginX;

        subFont.GetFontMetrics(out var sub);
        float subLineHeight     = -sub.Ascent + sub.Descent;
        float dateRangeBaseline = LayoutConstants.HeaderHeight - 20f - sub.Descent;
        float dasIstLosBaseline = dateRangeBaseline - subLineHeight;

        float blockTop = dasIstLosBaseline + sub.Ascent;
        titleFont.GetFontMetrics(out var title);
        float titleBaseline = blockTop / 2f - (title.Ascent + title.Descent) / 2f;

        canvas.DrawText("Kultur in Bocholt", x, titleBaseline, SKTextAlign.Left, titleFont, titlePaint);
        canvas.DrawText("Das ist los!", x, dasIstLosBaseline, SKTextAlign.Left, subFont, subPaint);

        string dateRange = $"vom {DateFormatHelper.FormatShortDate(startDate)} - {DateFormatHelper.FormatShortDate(endDate)}";
        canvas.DrawText(dateRange, x, dateRangeBaseline, SKTextAlign.Left, subFont, subPaint);
    }

    private static float DrawDayGroup(SKCanvas canvas, DayGroup day, float currentY)
    {
        currentY += LayoutConstants.DayHeaderTopPadding;

        using var headerFont  = MakeFont(38, bold: true);
        using var headerPaint = MakeTextPaint(DarkText);
        headerFont.GetFontMetrics(out var m);
        float labelY = currentY + LayoutConstants.DayHeaderHeight / 2f - (m.Ascent + m.Descent) / 2f;
        canvas.DrawText(day.DayName, LayoutConstants.MarginX, labelY, SKTextAlign.Left, headerFont, headerPaint);

        currentY += LayoutConstants.DayHeaderHeight;

        foreach (var ev in day.Events)
            currentY = DrawEventEntry(canvas, ev, currentY);

        return currentY;
    }

    private static float DrawEventEntry(SKCanvas canvas, ChaynsEvent ev, float currentY)
    {
        float iconRadius = LayoutConstants.IconDiameter / 2f;
        float iconCx     = LayoutConstants.MarginX + iconRadius;
        float iconCy     = currentY + LayoutConstants.EventTitleRowHeight / 2f;

        using var iconStroke = new SKPaint
        {
            Color       = IconColor,
            IsAntialias = true,
            IsStroke    = true,
            StrokeWidth = 4.5f,
        };
        using var arrowPaint = new SKPaint
        {
            Color       = IconColor,
            IsAntialias = true,
            IsStroke    = true,
            StrokeWidth = 4.5f,
            StrokeCap   = SKStrokeCap.Round,
        };

        canvas.DrawCircle(iconCx, iconCy, iconRadius, iconStroke);

        float tipX      = iconCx + iconRadius - 12f;
        float shaftLeft = iconCx - iconRadius + 12f;
        canvas.DrawLine(shaftLeft, iconCy, tipX, iconCy, arrowPaint);
        canvas.DrawLine(tipX, iconCy, tipX - 11f, iconCy - 11f, arrowPaint);
        canvas.DrawLine(tipX, iconCy, tipX - 11f, iconCy + 11f, arrowPaint);

        float textX  = LayoutConstants.MarginX + LayoutConstants.IconDiameter + 16f;
        float availW = LayoutConstants.ImageWidth - textX - LayoutConstants.MarginX;

        using var titleFont     = MakeFont(35);
        using var titlePaint    = MakeTextPaint(DarkText);
        using var locationFont  = MakeFont(27);
        using var locationPaint = MakeTextPaint(GrayText);

        titleFont.GetFontMetrics(out var tm);
        float titleBaseline = currentY + LayoutConstants.EventTitleRowHeight / 2f
                              - (tm.Ascent + tm.Descent) / 2f;

        DrawWrappedText(canvas, ev.Title, textX, titleBaseline, availW, titleFont, titlePaint);

        string locationLine = $"{ev.Location}, {ev.StartDate:HH:mm} Uhr";
        float  locY         = currentY + LayoutConstants.EventTitleRowHeight + 26f;
        canvas.DrawText(locationLine, textX, locY, SKTextAlign.Left, locationFont, locationPaint);

        return currentY
             + LayoutConstants.EventTitleRowHeight
             + LayoutConstants.EventLocationRowHeight
             + LayoutConstants.EventSpacing;
    }

    private static void DrawFooter(SKCanvas canvas, bool isLastPage)
    {
        float rightX  = LayoutConstants.ImageWidth - LayoutConstants.MarginX;
        float bottomY = LayoutConstants.ImageHeight - 14f;

        using var footerFont  = MakeFont(24);
        using var footerPaint = MakeTextPaint(GrayText);
        const string website  = "kultur-in-bocholt.de";
        float websiteW        = footerFont.MeasureText(website);
        canvas.DrawText(website, rightX - websiteW, bottomY, SKTextAlign.Left, footerFont, footerPaint);

        if (isLastPage) return;

        using var mehrFont  = MakeFont(30);
        using var mehrPaint = MakeTextPaint(DarkText);
        const string mehr   = "Mehr";
        float mehrW         = mehrFont.MeasureText(mehr);
        mehrFont.GetFontMetrics(out var mm);

        float mehrLineHeight = -mm.Ascent + mm.Descent;
        float mehrY          = bottomY - mehrLineHeight - 6f;

        float triSize = 22f;
        float triX    = rightX - triSize;
        float triMidY = mehrY + (mm.Ascent + mm.Descent) / 2f;

        using var triFill = new SKPaint { Color = IconColor, IsAntialias = true };
        using var tri     = new SKPath();
        tri.MoveTo(triX,           triMidY - triSize);
        tri.LineTo(triX,           triMidY + triSize);
        tri.LineTo(triX + triSize, triMidY);
        tri.Close();
        canvas.DrawPath(tri, triFill);

        canvas.DrawText(mehr, triX - mehrW - 10f, mehrY, SKTextAlign.Left, mehrFont, mehrPaint);
    }

    private static void DrawWrappedText(
        SKCanvas canvas, string text, float x, float y,
        float maxWidth, SKFont font, SKPaint paint)
    {
        var    words     = text.Split(' ');
        string line      = string.Empty;
        font.GetFontMetrics(out var m);
        float lineHeight = -m.Ascent + m.Descent + 4f;

        foreach (var word in words)
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureText(candidate) > maxWidth && line.Length > 0)
            {
                canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
                y   += lineHeight;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
    }

    private static SKFont MakeFont(float size, bool bold = false)
        => new(
            SKTypeface.FromFamilyName(
                "sans-serif",
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
            size);

    private static SKPaint MakeTextPaint(SKColor color)
        => new() { Color = color, IsAntialias = true };
}
