using System.Linq;
using System.Net;
using System.Net.Http.Json;
using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrownEvents.Tests;

public class DbContextConcurrencyTests
{
    // Regression test for the Singleton AppDbContext bug: DbContext/ChangeTracker isn't
    // thread-safe, so a shared instance races under concurrent requests. The lifetime assertion
    // below reads Program.cs's actual AppDbContext registration before swapping in the
    // InMemory-backed one, so this test fails if Program.cs ever regresses to AddSingleton
    // instead of just testing the DI shape hardcoded here.
    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                Assert.NotNull(descriptor);
                Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
                services.Remove(descriptor);

                services.AddScoped<AppDbContext>(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase("DbContextConcurrencyTestsDb")
                        .Options;
                    return new AppDbContext(options);
                });
            });
        });
    }

    [Fact]
    public async Task ConcurrentRequests_AllSucceed()
    {
        using var factory = CreateFactory();

        int conferenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conference = new Conference { Title = "Concurrency Test Conference", Description = "seed" };
            context.Conferences.Add(conference);
            await context.SaveChangesAsync();
            conferenceId = conference.Id;
        }

        using var client = factory.CreateClient();

        var requests = Enumerable.Range(0, 40).Select(i =>
        {
            return (i % 3) switch
            {
                0 => (Kind: i % 3, Task: client.GetAsync("/api/conferences")),
                1 => (Kind: i % 3, Task: client.GetAsync($"/api/conferences/{conferenceId}")),
                _ => (Kind: i % 3, Task: client.PostAsJsonAsync($"/api/conferences/{conferenceId}/register",
                        new Attendee { FirstName = "Attendee", LastName = $"{i}", Email = $"attendee{i}@example.com" })),
            };
        }).ToList();

        var responses = await Task.WhenAll(requests.Select(r => r.Task));

        for (var i = 0; i < requests.Count; i++)
        {
            var expected = requests[i].Kind == 2 ? HttpStatusCode.Created : HttpStatusCode.OK;
            Assert.Equal(expected, responses[i].StatusCode);
        }
    }
}
