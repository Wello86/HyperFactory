namespace BrownEvents.Api.Dtos;

public class SessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public int ConferenceId { get; set; }
    public int? SpeakerId { get; set; }
    public int? RoomId { get; set; }
    public SpeakerDto? Speaker { get; set; }
    public RoomDto? Room { get; set; }
    public int RegisteredCount { get; set; }
    public int WaitlistCount { get; set; }
    public int? CurrentAttendeeRegistrationId { get; set; }
}
