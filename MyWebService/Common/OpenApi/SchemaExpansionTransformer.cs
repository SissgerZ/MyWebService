//using Microsoft.AspNetCore.OpenApi;
//using Microsoft.OpenApi;

//namespace MyWebService.Common.OpenApi;

//public sealed class SchemaExpansionTransformer : IOpenApiSchemaTransformer
//{
//    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
//    {
//        var type = context.JsonTypeInfo.Type;

//        // In Microsoft.OpenApi 3.0.0, we check if it's already a reference
//        // and ensure we are dealing with a valid type.
//        if (type is not null && type != typeof(object))
//        {
//            string schemaId = type.Name;

//            // Handle Generic Types (e.g., Command<Response> -> CommandResponse)
//            if (type.IsGenericType)
//            {
//                var genericArgs = string.Join("", type.GetGenericArguments().Select(t => t.Name));
//                schemaId = $"{type.Name.Split('`')[0]}{genericArgs}";
//            }

//            // To fix the "dots" in Swagger UI, we force the schema to
//            // point to a Component Reference ID.
//            schema.Reference = new OpenApiReference
//            {
//                Id = schemaId,
//                Type = ReferenceType.Schema
//            };
//        }

//        return Task.CompletedTask;
//    }
//}