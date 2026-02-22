using ErrorOr;
using MediatR;

namespace MyWebService.Featrues.Products.CreateProduct;

/// <param name="Name">The unique name of the product. Must be globally unique.</param>
/// <param name="Description">Detailed specifications and marketing copy for the product.</param>
/// <param name="Price">The retail price of the product in USD.</param>
public record Command(
    string Name,
    string Description,
    decimal Price
) : IRequest<ErrorOr<Response>>;

//public record Command(
//    [property: Description("The unique name of the product. Must be globally unique.")]
//    [property: DefaultValue(typeof(string), "Mechanical Gaming Keyboard")]
//    string Name,

//    [property: Description("Detailed specifications and marketing copy for the product.")]
//    [property: DefaultValue(typeof(string), "A high-performance mechanical keyboard...")]
//    string Description,

//    [property: Description("The retail price of the product in USD.")]
//    [property: DefaultValue(typeof(decimal), "129.99")]
//    decimal Price
//) : IRequest<ErrorOr<Response>>;