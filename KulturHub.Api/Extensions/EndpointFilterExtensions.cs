using KulturHub.Api.Filters;

namespace KulturHub.Api.Extensions;

public static class EndpointFilterExtensions
{
    public static RouteHandlerBuilder WithUnitOfWork(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<UnitOfWorkEndpointFilter>();
    }
}
