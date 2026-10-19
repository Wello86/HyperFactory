using System.ComponentModel.DataAnnotations;

namespace BrownEvents.Api.Models;

public class Attendee
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    public virtual ICollection<SessionRegistration> SessionRegistrations { get; set; } = new List<SessionRegistration>();
}
