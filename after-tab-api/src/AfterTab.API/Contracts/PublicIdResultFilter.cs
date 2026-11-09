using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AfterTab.API.Contracts;

public sealed class PublicIdResultFilter(PublicIds ids) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result && (result.StatusCode ?? 200) < 400)
        {
            result.Value = await ids.ResponseAsync(result.Value, context.HttpContext.RequestAborted);
            result.DeclaredType = null;
        }
        await next();
    }
}
