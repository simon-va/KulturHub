using FluentValidation;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Abstractions.Pagination;
using KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;
using KulturHub.Domain.ChangeLogs;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform;

public static class ChangeLogEndpoints
{
    public static IEndpointRouteBuilder MapChangeLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organisations/{organisationId:guid}/change-logs")
            .WithTags("ChangeLogs")
            .WithGroupName("platform")
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>();

        group.MapGet("/", async (
            Guid organisationId,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            [FromQuery] string? search,
            [FromQuery] short? category,
            [FromServices] IValidator<ListChangeLogsRequest> validator,
            [FromServices] ListChangeLogsHandler handler,
            CancellationToken ct) =>
        {
            var request = new ListChangeLogsRequest(
                skip ?? 0,
                take ?? 50,
                search,
                category is null ? null : (ChangeLogCategory)category.Value);

            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                var errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }

            var command = new ListChangeLogsCommand(
                organisationId,
                request.Skip,
                request.Take,
                request.Search,
                request.Category);

            var result = await handler.HandleAsync(command, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status200OK),
                errors => errors.ToResult());
        })
            .Produces<PagedResult<ChangeLogResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("ChangeLogs_ListByOrganisation");

        return app;
    }
}