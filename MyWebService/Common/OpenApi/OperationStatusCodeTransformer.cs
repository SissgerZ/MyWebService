using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MyWebService.Common.OpenApi;

public sealed class OperationStatusCodeTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var response in operation.Responses ?? [])
        {
            // Attempt to parse the OpenAPI response key (e.g. "200", "400") into an integer
            if (!int.TryParse(response.Key, out var statusCode))
                continue;

            // Apply standard global descriptions
            response.Value.Description = statusCode switch
            {
                StatusCodes.Status201Created => "The resource was successfully created.",
                StatusCodes.Status400BadRequest => "Validation failed. Please check the provided data.",
                StatusCodes.Status404NotFound => "The requested resource could not be found.",
                StatusCodes.Status409Conflict => "A conflict occurred, such as a duplicate resource.",
                StatusCodes.Status500InternalServerError => "An unexpected error occurred on the server.",
                _ => response.Value.Description // Keeps the default if not explicitly matched
            };
        }

        return Task.CompletedTask;
    }
}