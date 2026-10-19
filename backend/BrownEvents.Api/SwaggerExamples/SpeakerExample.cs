using BrownEvents.Api.Models;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.SwaggerExamples;

public class SpeakerExample : IExamplesProvider<Speaker>
{
    public Speaker GetExamples() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Bio = "Principal engineer specializing in distributed systems and platform engineering.",
        Email = "jane.doe@example.com"
    };
}
