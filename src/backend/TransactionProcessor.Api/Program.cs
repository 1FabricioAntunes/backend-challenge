using FastEndpoints;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSwag;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionProcessor.Application.Queries.Files;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration
// Get connection string from appsettings or environment variables
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres";

// Configure Entity Framework Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories (Dependency Injection)
builder.Services.AddScoped<IFileRepository, FileRepository>();

// Register MediatR for query/command handling
builder.Services.AddMediatR(config =>
    config.RegisterServicesFromAssemblyContaining<GetFilesQuery>());

// Services: FastEndpoints DI
builder.Services.AddFastEndpoints();

// JSON serialization (camelCase, enums as strings, ignore nulls)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // ISO 8601 UTC format for DateTime serialization
    opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Swagger/OpenAPI (NSwag) - v1 document
builder.Services.AddOpenApiDocument(settings =>
{
    settings.Title = "TransactionProcessor API";
    settings.Version = "v1";
});

var app = builder.Build();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
    });
}

app.UseHttpsRedirection();

// FastEndpoints
app.UseFastEndpoints();

app.Run();
