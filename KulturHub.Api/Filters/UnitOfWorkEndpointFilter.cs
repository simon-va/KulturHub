using KulturHub.Application.Ports;

namespace KulturHub.Api.Filters;

public class UnitOfWorkEndpointFilter(IUnitOfWork unitOfWork) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        await unitOfWork.BeginAsync();
        try
        {
            var result = await next(context);
            await unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }
}
