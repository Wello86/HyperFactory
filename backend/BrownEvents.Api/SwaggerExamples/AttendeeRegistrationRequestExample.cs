using BrownEvents.Api.Dtos;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.SwaggerExamples;

public class AttendeeRegistrationRequestExample : IExamplesProvider<AttendeeRegistrationRequestDto>
{
    public AttendeeRegistrationRequestDto GetExamples() => new()
    {
        FirstName = "John",
        LastName = "Smith",
        Email = "john.smith@example.com",
    };
}
