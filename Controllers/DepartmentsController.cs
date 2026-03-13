using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCPCMvc.Data;
using QCPCMvc.Models;

namespace QCPCMvc.Controllers;

[Authorize(Roles = "Admin")]
public class DepartmentsController : Controller
{
    private readonly AppDbContext _db;

    public DepartmentsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: Departments
    public async Task<IActionResult> Index()
    {
        var departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
        return View(departments);
    }

    // POST: Departments/Create (inline from table)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Department department)
    {
        if (string.IsNullOrWhiteSpace(department.Name))
        {
            TempData["Error"] = "Department name is required.";
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            department.CreatedAt = DateTime.UtcNow;
            department.IsActive = true;
            _db.Departments.Add(department);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Department created successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: Departments/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        
        var department = await _db.Departments.FindAsync(id);
        if (department == null) return NotFound();
        
        return View(department);
    }

    // POST: Departments/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Department department)
    {
        if (id != department.Id) return NotFound();
        
        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(department);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Department updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(department.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(department);
    }

    // POST: Departments/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var department = await _db.Departments.FindAsync(id);
        if (department != null)
        {
            // Check if any users are assigned to this department
            var usersCount = await _db.Users.CountAsync(u => u.DepartmentId == id);
            if (usersCount > 0)
            {
                TempData["Error"] = $"Cannot delete department. {usersCount} user(s) are assigned to this department.";
                return RedirectToAction(nameof(Index));
            }
            
            _db.Departments.Remove(department);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Department deleted successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool DepartmentExists(int id)
    {
        return _db.Departments.Any(e => e.Id == id);
    }
}
