using System.Text.Json.Nodes;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Users;
using AfterTab.Mcp;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace AfterTab.API.Mcp;

public sealed class McpWriteStore(AppDbContext db, ICurrentUser currentUser) : IMcpWriteStore
{
    public async Task<JsonNode?> ExecuteAsync(Guid operationId, string tool, string fingerprint,
        Func<Task<JsonNode?>> operation, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new McpException("Authentication is required.");
        var existing = await Find(userId, operationId, ct);
        if (existing is not null) return Replay(existing, tool, fingerprint);

        var record = new McpOperation { UserId = userId, OperationId = operationId, Tool = tool, Fingerprint = fingerprint };
        db.McpOperations.Add(record);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(record).State = EntityState.Detached;
            // A competing request may have reserved this operation. Do not mask other DB errors.
            existing = await Find(userId, operationId, ct);
            if (existing is null) throw;
            return Replay(existing, tool, fingerprint);
        }

        // The reservation is committed first. On any failure it stays incomplete: the client
        // must inspect state instead of blindly duplicating a possibly committed expense.
        var result = await operation();
        // Business repositories can clear the tracker during their own transactions.
        db.ChangeTracker.Clear();
        record = await db.McpOperations.SingleAsync(x => x.UserId == userId && x.OperationId == operationId, ct);
        record.ResultJson = result?.ToJsonString() ?? "null";
        record.Completed = true;
        await db.SaveChangesAsync(ct);
        return result;
    }

    private Task<McpOperation?> Find(long userId, Guid operationId, CancellationToken ct)
        => db.McpOperations.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.OperationId == operationId, ct);
    private static JsonNode? Replay(McpOperation record, string tool, string fingerprint)
    {
        if (record.Tool != tool || record.Fingerprint != fingerprint)
            throw new McpException("This operationId belongs to a different request. Read the current state before proposing a new change.");
        if (!record.Completed)
            throw new McpException("This operation is pending or its outcome is uncertain. Read the current event/expenses before making another change. Do not automatically retry with a new operationId.");
        return JsonNode.Parse(record.ResultJson!);
    }
}
