using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public interface ISpeakerService
{
    Task<List<Speaker>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Speaker?> GetByIdWithSessionsAsync(int id, CancellationToken cancellationToken = default);
    Task<Speaker> CreateAsync(Speaker speaker, CancellationToken cancellationToken = default);
    Task<Speaker?> UpdateAsync(int id, Speaker speaker, CancellationToken cancellationToken = default);
}
