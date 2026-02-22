using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using MyWebService.Api.Infrastructure.Data;
using MyWebService.Common.ExceptionHandler;
using MyWebService.Common.OpenApi;
using MyWebService.Common.PipelineBehaviour;
using MyWebService.Common.ProblemDetails;
using MyWebService.Domain.Abstractions;
using Serilog;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 0. HOST & LOGGING SETUP (SERILOG)
// ==========================================
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Async(wt => wt.Console());
});

// ==========================================
// 1. BUILDER & INFRASTRUCTURE SETUP
// ==========================================
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.AllowResponseHeaderCompression = true;
    options.ConfigureEndpointDefaults(o => o.Protocols = HttpProtocols.Http1AndHttp2AndHttp3);
});

builder.Services.AddResponseCompression()
    .AddResponseCaching(options => options.MaximumBodySize = 1024)
    .AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddOutputCache(
    options => options.AddBasePolicy(policyBuilder => policyBuilder
        .Expire(TimeSpan.FromSeconds(10)).SetVaryByQuery("*")
    ));

builder.Services.Configure<FormOptions>(o =>
{
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartBodyLengthLimit = int.MaxValue;
    o.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.AddRequestTimeouts();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BookShopInMemoryDb"));

// ==========================================
// 2. SERVICES SETUP
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<DocumentTransformer>();

builder.Services.AddOpenApi("v1", options =>
{
    // Use the FullName (namespace + class name) to include the domain
    // This also forces schemas into the 'components' section, enabling expansion buttons.
    options.CreateSchemaReferenceId = (jsonTypeInfo) =>
    {
        var type = jsonTypeInfo.Type;

        // 1. Skip granular primitive types (string, int, bool, etc.)
        // Returning null tells the generator NOT to create a separate schema reference for them.
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return null;
        }

        // 2. Handle your Domain objects and Commands
        // We use the Namespace + Name to give you the "Domain" visibility you want.
        string schemaId = type.FullName ?? type.Name;

        // 3. Clean up Generic names (e.g., Command<Response> -> CommandOfResponse)
        if (type.IsGenericType)
        {
            var genericArgs = string.Join("", type.GetGenericArguments().Select(t => t.Name));
            schemaId = $"{type.Namespace}.{type.Name.Split('`')[0]}Of{genericArgs}";
        }

        return schemaId.Replace("+", ".");
    };

    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer<DocumentTransformer>();
    options.AddOperationTransformer<OperationStatusCodeTransformer>();
});

builder.Services.AddOpenApi("v2", options =>
{
    // Use the FullName (namespace + class name) to include the domain
    // This also forces schemas into the 'components' section, enabling expansion buttons.
    options.CreateSchemaReferenceId = (jsonTypeInfo) =>
    {
        var type = jsonTypeInfo.Type;

        // 1. Skip granular primitive types (string, int, bool, etc.)
        // Returning null tells the generator NOT to create a separate schema reference for them.
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return null;
        }

        // 2. Handle your Domain objects and Commands
        // We use the Namespace + Name to give you the "Domain" visibility you want.
        string schemaId = type.FullName ?? type.Name;

        // 3. Clean up Generic names (e.g., Command<Response> -> CommandOfResponse)
        if (type.IsGenericType)
        {
            var genericArgs = string.Join("", type.GetGenericArguments().Select(t => t.Name));
            schemaId = $"{type.Namespace}.{type.Name.Split('`')[0]}Of{genericArgs}";
        }

        return schemaId.Replace("+", ".");
    };

    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer<DocumentTransformer>();
    options.AddOperationTransformer<OperationStatusCodeTransformer>();
});


builder.Services.AddProblemDetails()
                .AddSingleton<IDeveloperPageExceptionFilter, DeveloperPageExceptionFilter>();

builder.Services.AddExceptionHandler<DefaultExceptionHandler>();

// Module Services Setup (Mediator & Endpoints)
List<Assembly> assemblies = [typeof(Program).Assembly];

// Add Mediator
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies([.. assemblies]);
    // NOTE: LoggingBehavior is removed; relying on Serilog Request Logging instead
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequestValidationPipelineBehavior<,>), ServiceLifetime.Scoped);
});

// Add Endpoints via Reflection
foreach (var serviceDescriptors in assemblies.Select(assembly => assembly
             .DefinedTypes
             .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                            type.IsAssignableTo(typeof(IEndpoint)))
             .Select(type => ServiceDescriptor.Scoped(typeof(IEndpoint), type))
             .ToArray()))
{
    builder.Services.TryAddEnumerable(serviceDescriptors);
}

// Add Custom CORS policy
builder.Services.AddCors(options => options.AddPolicy("api",
    policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
));

// ==========================================
// DIRECT RATE LIMITING SETUP
// ==========================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("PerUserRatelimit", context =>
    {
        // Fallback to IP address if the user is not authenticated
        var partitionKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey,
            _ => new TokenBucketRateLimiterOptions
            {
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                AutoReplenishment = true,
                TokenLimit = 100,
                TokensPerPeriod = 100,
                QueueLimit = 100
            });
    });
});

// ==========================================
// 3. MIDDLEWARE PIPELINE
// ==========================================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseOutputCache()
       .UseRequestTimeouts()
       .UseResponseCompression()
       .UseResponseCaching()
       .UseHttpsRedirection();

    // Classic Swagger UI pointing to Native .NET 10 endpoints
    app.UseSwaggerUI(opt =>
    {
        foreach (var (url, name) in from ApiVersionDescription desc
                                    in app.DescribeApiVersions()
                                    let url = $"/openapi/{desc.GroupName}.json"
                                    let name = desc.GroupName.ToUpperInvariant()
                                    select (url, name))
        {
            opt.SwaggerEndpoint(url, name);
        }

        opt.DocumentTitle = "MyWebService";

        opt.DefaultModelExpandDepth(2);
        opt.DefaultModelsExpandDepth(1);

        opt.DisplayRequestDuration();
        opt.EnableFilter();
        opt.EnableValidator();
        opt.EnableTryItOutByDefault();
        opt.EnablePersistAuthorization();
    });

    app.UseDeveloperExceptionPage()
       .UseStatusCodePages();
    app.UseExceptionHandler();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCors("api");

// ==========================================
// SERILOG REQUEST LOGGING
// ==========================================
app.UseSerilogRequestLogging(options =>
{
    // Customize the message template to include the endpoint name
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    // Optionally enrich the logs with additional HTTP context data
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);

        var endpoint = httpContext.GetEndpoint();
        if (endpoint != null)
        {
            diagnosticContext.Set("EndpointName", endpoint.DisplayName);
        }
    };
});

// If you ever add Auth, make sure app.UseAuthentication() and app.UseAuthorization()
// are placed right here, BEFORE app.UseRateLimiter(), so the Limiter can read the User claims!

app.UseRateLimiter();
app.UseHttpsRedirection();

// ==========================================
// 4. ENDPOINT MAPPING
// ==========================================

if (app.Environment.IsDevelopment())
{
    // Expose the raw OpenAPI JSON files
    app.MapOpenApi();
}

// Map standard endpoints dynamically
using (var scope = app.Services.CreateScope())
{
    var endpoints = scope.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>();

    var apiVersionSet = app
        .NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1))
        .HasApiVersion(new ApiVersion(2))
        .ReportApiVersions()
        .Build();

    IEndpointRouteBuilder routeBuilder = app
        .MapGroup("/api/v{apiVersion:apiVersion}")
        .WithApiVersionSet(apiVersionSet)
        .RequireRateLimiting("PerUserRatelimit"); // Applies rate limit to all mapped endpoints

    foreach (var endpoint in endpoints)
    {
        endpoint.MapEndpoint(routeBuilder);
    }
}

app.Map("/", () => Results.Redirect("/swagger"));

app.Map("/error", () => Results.Problem(
    "An unexpected error occurred.",
    statusCode: StatusCodes.Status500InternalServerError
)).ExcludeFromDescription();

app.Run();