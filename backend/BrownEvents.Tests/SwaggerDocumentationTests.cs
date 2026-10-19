using System.Linq;
using System.Net.Http.Json;
using BrownEvents.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrownEvents.Tests;

public class SwaggerDocumentationTests
{
    // method + path for every endpoint that should exist in the app.
    // Keeping this list explicit means a route silently missing from the
    // Swagger document (e.g. a controller that isn't picked up, or a new
    // endpoint added without documentation) fails this test.
    private static readonly (string Method, string Path)[] ExpectedRoutes = new[]
    {
        ("get", "/api/conferences"),
        ("get", "/api/conferences/{id}"),
        ("post", "/api/conferences"),
        ("put", "/api/conferences/{id}"),
        ("get", "/api/conferences/{id}/sessions"),
        ("post", "/api/conferences/{id}/sessions"),
        ("get", "/api/conferences/{id}/registrations"),
        ("post", "/api/conferences/{id}/register"),
        ("delete", "/api/conferences/{id}/registrations/{registrationId}"),
        ("get", "/api/sessions/{id}"),
        ("put", "/api/sessions/{id}"),
        ("get", "/api/speakers"),
        ("get", "/api/speakers/{id}"),
        ("post", "/api/speakers"),
        ("get", "/api/rooms"),
        ("get", "/api/attendees/{email}/registrations"),
    };

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddSingleton<AppDbContext>(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase("SwaggerDocumentationTestsDb")
                        .Options;
                    return new AppDbContext(options);
                });
            });
        });
    }

    [Fact]
    public async Task SwaggerDocument_ContainsEveryExpectedRoute()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonDocumentWrapper>("/swagger/v1/swagger.json");
        Assert.NotNull(document);

        var paths = document!.Paths;
        Assert.NotNull(paths);

        foreach (var (method, path) in ExpectedRoutes)
        {
            Assert.True(paths!.ContainsKey(path), $"Swagger document is missing path '{path}'.");
            var operations = paths[path];
            Assert.True(operations.ContainsKey(method), $"Swagger document is missing '{method.ToUpper()} {path}'.");

            var operation = operations[method];
            Assert.False(string.IsNullOrWhiteSpace(operation.Summary), $"'{method.ToUpper()} {path}' is missing a summary/description.");
        }
    }

    private class JsonDocumentWrapper
    {
        public Dictionary<string, Dictionary<string, OperationWrapper>>? Paths { get; set; }
    }

    private class OperationWrapper
    {
        public string? Summary { get; set; }
    }
}
