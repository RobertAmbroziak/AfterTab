using AfterTab.Common.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AfterTab.API.Errors;

public sealed class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException error) return false;
        var status = error.Kind switch
        {
            ErrorKind.Validation => 400, ErrorKind.NotFound => 404,
            ErrorKind.Forbidden => 403, ErrorKind.Conflict => 409, _ => 500
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = error.Message }, ct);
        return true;
    }
}
