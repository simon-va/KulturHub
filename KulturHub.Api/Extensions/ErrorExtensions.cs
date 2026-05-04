using ErrorOr;

namespace KulturHub.Api.Extensions;

public static class ErrorExtensions
{
    public static IResult ToResult(this List<Error> errors)
    {
        if (errors.Count == 0)
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var errorDictionary = errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return Results.ValidationProblem(errorDictionary);
        }

        var firstError = errors.First(e => e.Type != ErrorType.Validation);

        return firstError.Type switch
        {
            ErrorType.NotFound => Results.Problem(
                title: firstError.Description,
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = firstError.Code }),

            ErrorType.Conflict => Results.Problem(
                title: firstError.Description,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = firstError.Code }),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized),

            ErrorType.Forbidden => Results.Problem(
                title: firstError.Description,
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = firstError.Code }),

            _ => Results.Problem(
                title: firstError.Description,
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?> { ["code"] = firstError.Code }),
        };
    }
}
