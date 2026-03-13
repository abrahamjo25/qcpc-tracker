using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QCPCMvc.Models;
using QCPCMvc.Services;

namespace QCPCMvc.Filters;

public class NotificationCountFilter : IAsyncActionFilter
{
    private readonly IssueService _svc;
    private readonly UserManager<ApplicationUser> _um;

    public NotificationCountFilter(IssueService svc, UserManager<ApplicationUser> um)
    { _svc = svc; _um = um; }

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        if (ctx.Controller is Controller controller && ctx.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var user = await _um.GetUserAsync(ctx.HttpContext.User);
            if (user != null)
                controller.ViewBag.UnreadCount = await _svc.GetUnreadCountAsync(user.Id);
        }
        await next();
    }
}
