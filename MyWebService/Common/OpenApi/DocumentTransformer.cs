using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MyWebService.Common.OpenApi;

internal sealed class DocumentTransformer(IHttpContextAccessor httpContextAccessor) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = $"MyWeb API {context.DocumentName}",
            Description = "A simple vertical slices project",
            Version = context.DocumentName, // Usually maps to "v1", "v2", etc.
            Contact = new OpenApiContact { Name = "Tester", Email = "Tester@Test.com" }
        };

        var httpReq = httpContextAccessor.HttpContext?.Request;
        if (httpReq != null)
        {
            var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            document.Servers =
            [
                new OpenApiServer
                {
                    Url = $"{httpReq.Scheme}://{httpReq.Host.Value}",
                    Description = $"{envName} Environment"
                }
            ];
        }

        // ==========================================
        // OPTIONAL: JWT BEARER CONFIGURATION
        // ==========================================
        // Uncomment this section if you want to restore the JWT Bearer setup in .NET 10
        /*
        var schemeName = "Bearer";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add(schemeName, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter the Bearer Authorization string as following: `Generated-JWT-Token`",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = schemeName
        });

        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeName }
                },
                Array.Empty<string>()
            }
        });
        */

        return Task.CompletedTask;
    }
}