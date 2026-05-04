using KulturHub.Domain.Models;

namespace KulturHub.Domain.Interfaces;

public interface IImageGenerator
{
    List<byte[]> GenerateWeeklyImages(List<ChaynsEvent> events, DateTime weekStart, DateTime weekEnd);
}
