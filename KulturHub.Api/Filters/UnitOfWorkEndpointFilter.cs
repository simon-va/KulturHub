using KulturHub.Application.Ports;
using Microsoft.AspNetCore.Http.HttpResults;

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

            if (IsErrorResult(result))
            {
                await unitOfWork.RollbackAsync();
                return result;
            }

            await unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private static bool IsErrorResult(object? result)
    {
        return result is ProblemHttpResult or ValidationProblem;
    }
}
