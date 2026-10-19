using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public interface IConferenceService
{
    Task<PagedResult<Conference>> GetAllAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken cancellationToken = default);
    Task<Conference?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Conference> CreateAsync(Conference conference, CancellationToken cancellationToken = default);
    Task<Conference?> UpdateAsync(int id, Conference conference, CancellationToken cancellationToken = default);
    Task<PagedResult<Session>> GetSessionsAsync(int conferenceId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Session> AddSessionAsync(int conferenceId, Session session, CancellationToken cancellationToken = default);
    Task<List<Registration>> GetRegistrationsAsync(int conferenceId, CancellationToken cancellationToken = default);
}
