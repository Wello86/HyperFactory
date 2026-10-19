namespace BrownEvents.Api.Models;

public class SessionRegistration
{
    public int Id { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "CONFIRMED";

    public int SessionId { get; set; }
    public int AttendeeId { get; set; }

    public virtual Session Session { get; set; } = null!;
    public virtual Attendee Attendee { get; set; } = null!;
}
