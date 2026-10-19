namespace BrownEvents.Api.Dtos;

public class SpeakerSessionDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ConferenceId { get; set; }
    public string ConferenceTitle { get; set; } = string.Empty;
}
