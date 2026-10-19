using System.ComponentModel.DataAnnotations;

namespace BrownEvents.Api.Models;

public class Speaker
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
