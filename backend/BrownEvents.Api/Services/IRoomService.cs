using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public interface IRoomService
{
    Task<List<Room>> GetAllAsync(CancellationToken cancellationToken = default);
}
