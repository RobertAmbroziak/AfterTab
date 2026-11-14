using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Interfaces.Expenses;

namespace AfterTab.DAL.Interfaces.Events;

public sealed record EventReadModel(Event Event, string OwnerName, int ParticipantCount);
public interface IEventRepository
{
    Task<(IReadOnlyList<EventReadModel> Items, int TotalCount)> ListAsync(long userId, int page, CancellationToken ct);
    Task<EventReadModel?> GetAsync(long id, CancellationToken ct);
    Task RenameAsync(long eventId, long ownerId, string name, CancellationToken ct);
    Task<bool> IsMemberAsync(long eventId, long userId, CancellationToken ct);
    Task<IReadOnlyList<ExpenseParticipant>> ParticipantsAsync(long eventId, CancellationToken ct);
    Task AddVirtualAsync(long eventId, long ownerId, string name, DateTimeOffset now, CancellationToken ct);
    Task RenameVirtualAsync(long eventId, long ownerId, long participantId, string name, CancellationToken ct);
    Task RemoveVirtualAsync(long eventId, long ownerId, long participantId, CancellationToken ct);
    Task LinkVirtualAsync(long eventId, long ownerId, long participantId, long targetParticipantId, CancellationToken ct);
    Task CreateAsync(Event ev, EventParticipant owner, EventInvite invite, CancellationToken ct);
    Task<EventInvite?> FindInviteAsync(string hash, CancellationToken ct);
    Task<long> JoinAsync(string hash, long userId, DateTimeOffset now, CancellationToken ct);
    Task RotateInviteAsync(long eventId, long ownerId, EventInvite? replacement, DateTimeOffset now, CancellationToken ct);
}
