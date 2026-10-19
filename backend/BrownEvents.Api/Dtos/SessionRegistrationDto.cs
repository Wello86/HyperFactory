namespace BrownEvents.Api.Dtos;

public class SessionRegistrationDto
{
    public int Id { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int AttendeeId { get; set; }
    public AttendeeDto Attendee { get; set; } = null!;
}
