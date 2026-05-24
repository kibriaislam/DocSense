using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Pgvector.EntityFrameworkCore;
using RagApi.Application.Behaviours;
using RagApi.Application.Interfaces;
using RagApi.Infrastructure;
using RagApi.Infrastructure.Parsing;
using RagApi.Infrastructure.Persistence;
using RagApi.Infrastructure.Options;
using RagApi.Middleware;
using Serilog;
using System.Threading.RateLimiting;
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
    builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection(ChunkingOptions.SectionName));
    builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(global::RagApi.Application.DependencyInjection).Assembly);
        cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    });

    // Application services registered here
    builder.Services.AddValidatorsFromAssembly(typeof(global::RagApi.Application.DependencyInjection).Assembly);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "API key using Bearer scheme. Example: \"Bearer {your-api-key}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "ApiKey"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Infrastructure services registered here
    builder.Services.AddHttpClient<RagApi.Infrastructure.Ollama.OllamaClientService>(client =>
        client.Timeout = TimeSpan.FromMinutes(2))
        .AddStandardResilienceHandler();
    builder.Services.AddScoped<IEmbeddingService, RagApi.Infrastructure.Ollama.OllamaEmbeddingService>();
    builder.Services.AddScoped<IGenerationService, RagApi.Infrastructure.Ollama.OllamaGenerationService>();
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Default"),
            npgsqlOptions => npgsqlOptions.UseVector()));
    builder.Services.AddScoped<IVectorRepository, RagApi.Infrastructure.Persistence.PgVectorRepository>();
    builder.Services.AddInfrastructure();

    builder.Services.AddSingleton<PdfDocumentParser>();
    builder.Services.AddSingleton<DocxDocumentParser>();
    builder.Services.AddSingleton<TxtDocumentParser>();
    builder.Services.AddSingleton<IEnumerable<IDocumentParser>>(sp => new IDocumentParser[]
    {
        sp.GetRequiredService<PdfDocumentParser>(),
        sp.GetRequiredService<DocxDocumentParser>(),
        sp.GetRequiredService<TxtDocumentParser>()
    });
    builder.Services.AddSingleton<CompositeDocumentParser>();
    builder.Services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<CompositeDocumentParser>());

    // Controllers registered here
    builder.Services.AddControllers();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { type = "rate_limit_exceeded", message = "Too many requests. Please try again later." },
                cancellationToken);
        };

        options.AddPolicy("query-limit", httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
    });

    var app = builder.Build();

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.UseMiddleware<ApiKeyMiddleware>();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        timestamp = DateTime.UtcNow
    }));

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
