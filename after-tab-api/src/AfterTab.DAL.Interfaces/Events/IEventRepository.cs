using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;

namespace AfterTab.DAL.Interfaces.Events;

public sealed record EventReadModel(Event Event, string OwnerName, int ParticipantCount);
public interface IEventRepository
{
    Task<IReadOnlyList<EventReadModel>> ListAsync(Guid userId, CancellationToken ct);
    Task<EventReadModel?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid eventId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<User>> ParticipantsAsync(Guid eventId, CancellationToken ct);
    Task CreateAsync(Event ev, EventParticipant owner, EventInvite invite, CancellationToken ct);
    Task<EventInvite?> FindInviteAsync(string hash, CancellationToken ct);
    Task<Guid> JoinAsync(string hash, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task RotateInviteAsync(Guid eventId, Guid ownerId, EventInvite? replacement, DateTimeOffset now, CancellationToken ct);
}
