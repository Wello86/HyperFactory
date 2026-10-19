namespace BrownEvents.Api.Dtos;

public class ConferenceDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Venue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? MaxAttendees { get; set; }
    public string? OrganizerName { get; set; }
    public string? Website { get; set; }
}
