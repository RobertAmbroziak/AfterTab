using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.DTO.Events;
using AfterTab.BL.Interfaces.Events;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Interfaces.Events;
using AfterTab.DAL.Interfaces.Users;

namespace AfterTab.BL.Services.Events;

public sealed class EventService(IEventRepository events, IUserRepository users, ICurrentUser actor, TimeProvider clock) : IEventService
{
    private async Task<Guid> ActorAsync(bool requireName, CancellationToken ct)
    {
        var user = actor.UserId is Guid id ? await users.GetByIdAsync(id, ct) : null;
        if (user?.Status != UserStatus.Active) throw new AppException(ErrorKind.Forbidden, "Zaloguj się ponownie.");
        if (requireName && !user.DisplayNameConfirmed) throw new AppException(ErrorKind.Conflict, "Najpierw podaj swoje imię.");
        return user.Id;
    }
    private static EventSummaryDto Summary(EventReadModel data, Guid? userId)
        => new(data.Event.Id, data.Event.Name, data.Event.Description, data.Event.Currency, data.OwnerName,
            data.Event.OwnerUserId == userId, data.Event.Status.ToString(), data.ParticipantCount);

    public async Task<IReadOnlyList<EventSummaryDto>> ListAsync(CancellationToken ct)
    {
        var id = await ActorAsync(false, ct);
        return (await events.ListAsync(id, ct)).Select(x => Summary(x, id)).ToList();
    }
    public async Task<EventDetailsDto> GetAsync(Guid id, CancellationToken ct)
    {
        var userId = await ActorAsync(false, ct);
        var data = await events.GetAsync(id, ct);
        if (data is null || (data.Event.OwnerUserId != userId && !await events.IsMemberAsync(id, userId, ct)))
            throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia na Twojej liście.");
        var participants = await events.ParticipantsAsync(id, ct);
        return new(Summary(data, userId), participants.Select(x => new EventMemberDto(x.Id, x.DisplayName, x.Id == data.Event.OwnerUserId))
            .OrderByDescending(x => x.IsOwner).ThenBy(x => x.DisplayName).ToList());
    }
    public async Task<CreatedEventDto> CreateAsync(CreateEventDto input, CancellationToken ct)
    {
        var userId = await ActorAsync(true, ct);
        var name = input.Name?.Trim();
        var description = input.Description?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150 || name.Any(char.IsControl))
            throw new AppException(ErrorKind.Validation, "Nazwa wydarzenia musi mieć od 1 do 150 znaków.");
        if (description?.Length > 2000) throw new AppException(ErrorKind.Validation, "Opis może mieć najwyżej 2000 znaków.");
        var ev = new Event { Name = name, Description = description, Currency = "PLN", OwnerUserId = userId };
        var token = Secret();
        await events.CreateAsync(ev, new EventParticipant { EventId = ev.Id, UserId = userId, JoinedAtUtc = clock.GetUtcNow() },
            new EventInvite { EventId = ev.Id, TokenHash = Hash(token) }, ct);
        return new(await GetAsync(ev.Id, ct), token);
    }
    public async Task<InviteTokenDto> RotateInviteAsync(Guid id, CancellationToken ct)
    {
        var userId = await ActorAsync(true, ct);
        var token = Secret();
        await events.RotateInviteAsync(id, userId, new EventInvite { EventId = id, TokenHash = Hash(token) }, clock.GetUtcNow(), ct);
        return new(token);
    }
    public async Task RevokeInviteAsync(Guid id, CancellationToken ct)
        => await events.RotateInviteAsync(id, await ActorAsync(false, ct), null, clock.GetUtcNow(), ct);
    public async Task<InvitePreviewDto> PreviewAsync(string token, CancellationToken ct)
    {
        var invite = await events.FindInviteAsync(ValidateToken(token), ct);
        if (invite is null || invite.RevokedAtUtc is not null || invite.ExpiresAtUtc <= clock.GetUtcNow())
            throw new AppException(ErrorKind.NotFound, "Zaproszenie jest nieprawidłowe, wygasło lub zostało unieważnione.");
        var data = await events.GetAsync(invite.EventId, ct);
        if (data is null || data.Event.Status != EventStatus.Active)
            throw new AppException(ErrorKind.NotFound, "To zaproszenie nie jest już aktywne.");
        var member = actor.UserId is Guid id && (data.Event.OwnerUserId == id || await events.IsMemberAsync(data.Event.Id, id, ct));
        return new(data.Event.Id, data.Event.Name, data.OwnerName, data.Event.Currency, member);
    }
    public async Task<EventDetailsDto> JoinAsync(string token, CancellationToken ct)
    {
        var id = await events.JoinAsync(ValidateToken(token), await ActorAsync(true, ct), clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    private static string ValidateToken(string? token)
        => token is { Length: 64 } && token.All(Uri.IsHexDigit) ? Hash(token.ToUpperInvariant())
            : throw new AppException(ErrorKind.NotFound, "Nieprawidłowy link lub kod zaproszenia.");
    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
