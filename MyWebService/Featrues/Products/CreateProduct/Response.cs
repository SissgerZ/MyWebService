namespace MyWebService.Featrues.Products.CreateProduct;


public record Response(
    Guid Id,
    string Name
);

//public record Response(
//    [property: Description("The unique identifier generated for the newly created product.")]
//    [property: DefaultValue(typeof(Guid), "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
//    Guid Id,

//    [property: Description("The assigned name of the product.")]
//    [property: DefaultValue(typeof(string), "Mechanical Gaming Keyboard")]
//    string Name
//);
