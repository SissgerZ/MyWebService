using ErrorOr;
using MediatR;
using MyWebService.Api.Infrastructure.Data;
using MyWebService.Domain.Entities;

namespace MyWebService.Featrues.Products.CreateProduct;

public class Handler(AppDbContext db) : IRequestHandler<Command, ErrorOr<Response>>
{
    public async Task<ErrorOr<Response>> Handle(Command request, CancellationToken cancellationToken)
    {
        bool isProductUnique = !db.Products.Any(p => p.Name == request.Name);
        if (!isProductUnique)
        {
            return Error.Conflict(
                code: "Product.DuplicateName",
                description: $"A product with the name '{request.Name}' already exists.");
        }

        // Map Command to Domain Entity
        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price
        };

        // Persist to Database
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        // Return the Response DTO
        return new Response(product.Id, product.Name);
    }
}
