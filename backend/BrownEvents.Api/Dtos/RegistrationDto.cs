namespace BrownEvents.Api.Dtos;

public class RegistrationDto
{
    public int Id { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ConferenceId { get; set; }
    public int AttendeeId { get; set; }
    public AttendeeDto Attendee { get; set; } = null!;
    public string ConferenceTitle { get; set; } = string.Empty;
    public DateTime ConferenceStartDate { get; set; }
    public DateTime ConferenceEndDate { get; set; }
}
