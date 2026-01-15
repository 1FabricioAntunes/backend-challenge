using FastEndpoints;
using NSwag;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Services: FastEndpoints DI
builder.Services.AddFastEndpoints();

// JSON serialization (camelCase, enums as strings, ignore nulls)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
