using BrownEvents.Api.Models;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.SwaggerExamples;

public class ConferenceExample : IExamplesProvider<Conference>
{
    public Conference GetExamples() => new()
    {
        Title = "DevCon 2026",
        Description = "Annual conference for developers covering cloud, AI, and platform engineering.",
        Location = "Austin, TX",
        Venue = "Austin Convention Center",
        StartDate = new DateTime(2026, 10, 12),
        EndDate = new DateTime(2026, 10, 14),
        Status = "UPCOMING",
        MaxAttendees = 500,
        OrganizerName = "Brown Events Inc.",
        Website = "https://devcon2026.example.com"
    };
}
