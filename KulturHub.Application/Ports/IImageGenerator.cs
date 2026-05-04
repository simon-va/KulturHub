using KulturHub.Domain.Models;

namespace KulturHub.Application.Ports;

public interface IImageGenerator
{
    List<byte[]> GenerateWeeklyImages(List<ChaynsEvent> events, DateTime weekStart, DateTime weekEnd);
}
