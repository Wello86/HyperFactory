using BrownEvents.Api.Models;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.SwaggerExamples;

public class SessionExample : IExamplesProvider<Session>
{
    public Session GetExamples() => new()
    {
        Title = "Scaling Microservices with .NET",
        Description = "A deep dive into patterns for scaling .NET microservices in production.",
        StartTime = new DateTime(2026, 10, 12, 10, 0, 0),
        EndTime = new DateTime(2026, 10, 12, 11, 0, 0),
        Capacity = 120,
        SpeakerId = 1,
        RoomId = 1
    };
}
