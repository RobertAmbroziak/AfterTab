using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.DTO.Users;
using AfterTab.Common.Errors;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.API.Contracts;

// Keep GUIDs at the HTTP boundary. Business services and database relationships use bigint.
public sealed class PublicIds(AppDbContext db)
{
    private enum Kind { User, Session, Event, Participant, Expense }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Slot(JsonObject Object, string Name, Kind Kind, long Id);

    public async Task<long> ResolveAsync<T>(Guid id, CancellationToken ct) where T : PublicEntity
        => await db.Set<T>().Where(x => x.PublicId == id).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia.");

    public async Task<long?> ParticipantAsync(Guid? id, CancellationToken ct)
        => id.HasValue ? await ResolveAsync<EventParticipant>(id.Value, ct) : null;

    public async Task<IReadOnlyList<ExpenseShareDto>> SharesAsync(IReadOnlyList<PublicExpenseShare> shares, CancellationToken ct)
    {
        if (shares is null || shares.Count > 1000 || shares.Any(x => x is null))
            throw new AppException(ErrorKind.Validation, "Nieprawidłowa lub powtórzona osoba w podziale.");
        var ids = shares.Select(x => x.ParticipantId).Distinct().ToArray();
        var map = await db.EventParticipants.Where(x => ids.Contains(x.PublicId))
            .ToDictionaryAsync(x => x.PublicId, x => x.Id, ct);
        if (ids.Any(x => !map.ContainsKey(x)))
            throw new AppException(ErrorKind.Validation, "Nieprawidłowa lub powtórzona osoba w podziale.");
        return shares.Select(x => new ExpenseShareDto(map[x.ParticipantId], x.AmountMinor, x.PercentageBps)).ToArray();
    }

    public async Task<JsonNode?> ResponseAsync(object? value, CancellationToken ct)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value, value.GetType(), Json);
        var slots = new List<Slot>();
        Collect(value, node, slots);
        // One query per entity kind, rather than one query per row in a settlement or expense list.
        foreach (var group in slots.GroupBy(x => x.Kind))
        {
            var ids = group.Select(x => x.Id).Distinct().ToArray();
            var map = group.Key switch
            {
                Kind.User => await ReadAsync<User>(ids, ct),
                Kind.Session => await ReadAsync<Session>(ids, ct),
                Kind.Event => await ReadAsync<Event>(ids, ct),
                Kind.Participant => await ReadAsync<EventParticipant>(ids, ct),
                Kind.Expense => await ReadAsync<Expense>(ids, ct),
                _ => throw new InvalidOperationException("Unknown public identifier kind.")
            };
            foreach (var slot in group)
                slot.Object[slot.Name] = JsonValue.Create(map[slot.Id]);
        }
        return node;
    }

    private Task<Dictionary<long, Guid>> ReadAsync<T>(long[] ids, CancellationToken ct) where T : PublicEntity
        => db.Set<T>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);

    private static void Collect(object? value, JsonNode? node, List<Slot> slots)
    {
        if (value is null || node is null) return;
        if (node is JsonArray array && value is IEnumerable items)
        {
            var i = 0;
            foreach (var item in items) Collect(item, array[i++], slots);
        }
        else if (node is JsonObject obj)
        {
            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                if (!obj.TryGetPropertyValue(name, out var child) || child is null) continue;
                var member = property.GetValue(value);
                if (member is long id && (property.Name == "Id" || property.Name.EndsWith("Id", StringComparison.Ordinal)))
                    slots.Add(new Slot(obj, name, IdentifierKind(value.GetType(), property.Name), id));
                else Collect(member, child, slots);
            }
        }
    }

    private static Kind IdentifierKind(Type type, string name) => name switch
    {
        "SessionId" => Kind.Session,
        "EventId" => Kind.Event,
        "ExpenseId" => Kind.Expense,
        "UserId" or "AuthorUserId" or "OwnerUserId" => Kind.User,
        "ParticipantId" or "PaidByParticipantId" or "BalanceParticipantId" or "FromParticipantId" or "ToParticipantId" or "TargetParticipantId" => Kind.Participant,
        "Id" when type == typeof(UserDto) => Kind.User,
        "Id" when type == typeof(EventSummaryDto) => Kind.Event,
        "Id" when type == typeof(ExpenseDto) => Kind.Expense,
        _ => throw new InvalidOperationException($"Missing public identifier mapping for {type.Name}.{name}.")
    };
}

public sealed record PublicExpenseShare(Guid ParticipantId, long AmountMinor = 0, int PercentageBps = 0);
public sealed record PublicCreateExpense(string Name, long AmountMinor, string SplitMethod,
    IReadOnlyList<PublicExpenseShare> Shares, bool Balance = false, Guid? PaidByParticipantId = null, Guid? BalanceParticipantId = null);
public sealed record PublicUpdateExpenseSplit(string SplitMethod, IReadOnlyList<PublicExpenseShare> Shares,
    bool Balance, string RowVersion, Guid? BalanceParticipantId = null);
public sealed record PublicUpdateExpensePayer(Guid PaidByParticipantId, string RowVersion);
public sealed record PublicUpdateExpenseBalance(Guid? ParticipantId, string RowVersion);
public sealed record PublicLinkParticipant(Guid TargetParticipantId);
public sealed record PublicMarkTransfer(int Version, Guid FromParticipantId, Guid ToParticipantId, long AmountMinor, string Side, bool Value);
