using ErrorOr;

namespace MyWebService.Common.ProblemDetails;

public static class ErrorOrExtensions
{
    public static IResult ToProblemDetails(this List<Error> errors, HttpContext httpContext)
    {
        if (errors.Count == 0)
        {
            return Results.Problem();
        }

        var instancePath = $"{httpContext.Request.Method} {httpContext.Request.Path}";

        if (errors.TrueForAll(error => error.Type == ErrorType.Validation))
        {
            var validationDictionary = errors
                .GroupBy(e => e.Code)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Description).ToArray()
                );

            return Results.ValidationProblem(
                validationDictionary,
                instance: instancePath,
                title: "Validation Error",
                detail: "One or more validation errors occurred."
            );
        }

        var firstError = errors[0];

        var statusCode = firstError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: firstError.Description,
            instance: instancePath,
            extensions: new Dictionary<string, object?>
            {
                { "errorCodes", errors.Select(e => e.Code) }
            });
    }
}