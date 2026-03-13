using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCPCMvc.Models;

namespace QCPCMvc.Controllers;

[Authorize(Policy = "Manager")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _um;
    public AdminController(UserManager<ApplicationUser> um) => _um = um;

    public async Task<IActionResult> Users()
    {
        var users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        var roles  = new Dictionary<string, string>();
        foreach (var u in users)
        {
            var r = await _um.GetRolesAsync(u);
            roles[u.Id] = r.FirstOrDefault() ?? "Developer";
        }
        ViewBag.Roles = roles;
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId, string role)
    {
        var user = await _um.FindByIdAsync(userId);
        if (user == null) return NotFound();
        var current = await _um.GetRolesAsync(user);
        await _um.RemoveFromRolesAsync(user, current);
        await _um.AddToRoleAsync(user, role);
        TempData["Success"] = $"Role updated to {role} for {user.FullName}.";
        return RedirectToAction("Users");
    }
}
