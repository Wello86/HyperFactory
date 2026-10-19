using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public interface ISessionService
{
    Task<Session?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Session?> UpdateAsync(int id, Session session, CancellationToken cancellationToken = default);
}
