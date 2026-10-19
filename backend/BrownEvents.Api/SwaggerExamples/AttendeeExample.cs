using BrownEvents.Api.Models;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.SwaggerExamples;

public class AttendeeExample : IExamplesProvider<Attendee>
{
    public Attendee GetExamples() => new()
    {
        FirstName = "John",
        LastName = "Smith",
        Email = "john.smith@example.com"
    };
}
