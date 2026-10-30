using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Interfaces.Expenses;

namespace AfterTab.DAL.Interfaces.Events;

public sealed record EventReadModel(Event Event, string OwnerName, int ParticipantCount);
public interface IEventRepository
{
    Task<(IReadOnlyList<EventReadModel> Items, int TotalCount)> ListAsync(Guid userId, int page, CancellationToken ct);
    Task<EventReadModel?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid eventId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<ExpenseParticipant>> ParticipantsAsync(Guid eventId, CancellationToken ct);
    Task AddVirtualAsync(Guid eventId, Guid ownerId, string name, DateTimeOffset now, CancellationToken ct);
    Task RenameVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, string name, CancellationToken ct);
    Task RemoveVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, CancellationToken ct);
    Task LinkVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, Guid targetParticipantId, CancellationToken ct);
    Task CreateAsync(Event ev, EventParticipant owner, EventInvite invite, CancellationToken ct);
    Task<EventInvite?> FindInviteAsync(string hash, CancellationToken ct);
    Task<Guid> JoinAsync(string hash, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task RotateInviteAsync(Guid eventId, Guid ownerId, EventInvite? replacement, DateTimeOffset now, CancellationToken ct);
}
