using System.Data;
using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IEventRepository
{
    Task CreateAsync(Event @event, IDbTransaction transaction);
}
