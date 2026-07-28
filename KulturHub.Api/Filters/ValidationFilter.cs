using FluentValidation;

namespace KulturHub.Api.Filters;

public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: $"Request body of type {typeof(TRequest).Name} was not provided.");

        var validation = await validator.ValidateAsync(
            request,
            context.HttpContext.RequestAborted);

        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}