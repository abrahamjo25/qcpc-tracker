using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QCPCMvc.Models;
using QCPCMvc.Services;

namespace QCPCMvc.Controllers;

public class HomeController : Controller
{
    private readonly IssueService _svc;
    private readonly UserManager<ApplicationUser> _um;

    public HomeController(IssueService svc, UserManager<ApplicationUser> um)
    { _svc = svc; _um = um; }

    // Dashboard is only useful when authenticated — guests go to public issue list
    public async Task<IActionResult> Index()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return RedirectToAction("Index", "Issues");

        var vm = await _svc.GetDashboardAsync();
        return View(vm);
    }
}
