using FluentValidation;
using MediatR;
using Microsoft.OpenApi.Models;
using RagApi.Application.Behaviours;
using RagApi.Infrastructure;
using RagApi.Infrastructure.Options;
using RagApi.Middleware;
using Serilog;

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
    builder.Services.AddInfrastructure();

    // Controllers registered here
    builder.Services.AddControllers();

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
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
