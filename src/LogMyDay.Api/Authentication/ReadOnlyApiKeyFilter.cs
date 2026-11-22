using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LogMyDay.Api.Authentication;

/// <summary>
/// Refuses a REST request that changes data when a read-only API key authenticated it. The MCP
/// endpoint checks a key's scope through its own policies; this covers every controller.
/// </summary>
public sealed class ReadOnlyApiKeyFilter : IAuthorizationFilter
{
    public const string Message = "This API key is read-only.";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return;
        }

        if (!ApiKeyAuthDefaults.IsReadOnlyKey(context.HttpContext.User))
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = Message
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
