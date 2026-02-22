using MediatR;
using MyWebService.Common.ProblemDetails;
using MyWebService.Domain.Abstractions;
using MyWebService.Featrues.Products.CreateProduct;

namespace MyWebService.Features.Products.CreateProduct;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (Command command, ISender sender, HttpContext httpContext) =>
        {
            var response = await sender.Send(command);

            return response.Match(
                success => Results.Created($"/api/v1/products/{success.Id}", success),
                errors => errors.ToProblemDetails(httpContext)
            );
        })
        .WithTags("Products")
        .WithName("CreateProduct")
        .MapToApiVersion(1)
        .WithSummary("Creates a new product in the catalog.")
        .WithDescription("Provides the ability to add a new product. The product name must be unique across the entire catalog.")
        .Produces<Response>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

