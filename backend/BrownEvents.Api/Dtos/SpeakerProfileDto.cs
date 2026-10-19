namespace BrownEvents.Api.Dtos;

public class SpeakerProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public List<SpeakerSessionDto> Sessions { get; set; } = new();
}
