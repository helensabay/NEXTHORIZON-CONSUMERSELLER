using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NextHorizon.Modules.MemberTracker.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ConditionalValidateAntiForgeryTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string RequireCsrfConfigKey = "UploadAuthorization:RequireCsrfForUploadWrites";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var requireCsrf = configuration.GetValue(RequireCsrfConfigKey, true);
        if (!requireCsrf)
        {
            return;
        }

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            });
        }
    }
}