using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCPCMvc.Data;
using QCPCMvc.Models;
using QCPCMvc.Services;
using QCPCMvc.ViewModels;

namespace QCPCMvc.Controllers;

public class IssuesController : Controller
{
    private readonly IssueService _svc;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _um;

    public IssuesController(IssueService svc, AppDbContext db, UserManager<ApplicationUser> um)
    { _svc = svc; _db = db; _um = um; }

    [AllowAnonymous]
    public async Task<IActionResult> Index(IssueFilterVm filter)
    {
        var vm = await _svc.GetListAsync(filter);
        ViewBag.Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        return View(vm);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Detail(int id)
    {
        var issue = await _svc.GetDetailAsync(id);
        if (issue == null) return NotFound();
        var vm = new IssueDetailVm
        {
            Issue = issue,
            Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync()
        };
        return View(vm);
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        return View(new IssueFormVm());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IssueFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return View(vm);
        }
        var user  = await _um.GetUserAsync(User);
        var issue = MapToIssue(vm);
        issue.SubmittedById = user!.Id;
        var created = await _svc.CreateAsync(issue, user.Id);
        TempData["Success"] = $"Issue #{created.ResponseNumber} submitted successfully.";
        return RedirectToAction("Detail", new { id = created.Id });
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var issue = await _db.Issues.FindAsync(id);
        if (issue == null) return NotFound();
        var user = await _um.GetUserAsync(User);
        if (issue.SubmittedById != user!.Id && !User.IsInRole("Admin") && !User.IsInRole("QualityManager"))
            return Forbid();
        ViewBag.Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        return View(MapToVm(issue));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IssueFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Users = await _um.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return View(vm);
        }
        var issue = await _db.Issues.FindAsync(vm.Id);
        if (issue == null) return NotFound();
        var user = await _um.GetUserAsync(User);
        ApplyVm(vm, issue);
        await _svc.UpdateAsync(issue, user!.Id);
        TempData["Success"] = "Issue updated.";
        return RedirectToAction("Detail", new { id = vm.Id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, IssueStatus status)
    {
        var user = await _um.GetUserAsync(User);
        await _svc.ChangeStatusAsync(id, status, user!.Id);
        TempData["Success"] = $"Status changed to {status}.";
        return RedirectToAction("Detail", new { id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string? assigneeId)
    {
        var user = await _um.GetUserAsync(User);
        await _svc.AssignAsync(id, assigneeId, user!.Id);
        TempData["Success"] = string.IsNullOrEmpty(assigneeId) ? "Issue unassigned." : "Issue assigned.";
        return RedirectToAction("Detail", new { id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostAnswer(int issueId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Answer cannot be empty.";
            return RedirectToAction("Detail", new { id = issueId });
        }
        var user    = await _um.GetUserAsync(User);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await _svc.AddAnswerAsync(issueId, body.Trim(), user!.Id, baseUrl);
        TempData["Success"] = "Answer posted.";
        return RedirectToAction("Detail", new { id = issueId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptAnswer(int answerId, int issueId)
    {
        var user = await _um.GetUserAsync(User);
        await _svc.AcceptAnswerAsync(answerId, user!.Id);
        TempData["Success"] = "Answer accepted!";
        return RedirectToAction("Detail", new { id = issueId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostComment(int issueId, int? answerId, string body, int? parentId)
    {
        if (string.IsNullOrWhiteSpace(body))
            return RedirectToAction("Detail", new { id = issueId });
        var user    = await _um.GetUserAsync(User);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await _svc.AddCommentAsync(
            answerId.HasValue ? null : issueId,
            answerId,
            body.Trim(), user!.Id, parentId, baseUrl);
        return RedirectToAction("Detail", new { id = issueId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MentionUser(int issueId, string mentionedUserId)
    {
        var actor   = await _um.GetUserAsync(User);
        var target  = await _um.FindByIdAsync(mentionedUserId);
        if (target == null) return RedirectToAction("Detail", new { id = issueId });
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        // Use dot-joined handle so RenderMentions can highlight it as a single token
        var handle  = target.FullName.Replace(" ", ".");
        await _svc.AddCommentAsync(issueId, null,
            $"@{handle} — could you please provide an answer to this issue?",
            actor!.Id, null, baseUrl);
        TempData["Success"] = $"{target.FullName} has been notified!";
        return RedirectToAction("Detail", new { id = issueId });
    }

    [Authorize]
    public async Task<IActionResult> Notifications()
    {
        var user  = await _um.GetUserAsync(User);
        var notifs= await _svc.GetNotificationsAsync(user!.Id);
        return View(notifs);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _svc.MarkNotifReadAsync(id);
        return RedirectToAction("Notifications");
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _um.GetUserAsync(User);
        await _svc.MarkAllReadAsync(user!.Id);
        return RedirectToAction("Notifications");
    }

    // ── Mention autocomplete API ───────────────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> MentionSuggest(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Json(Array.Empty<object>());

        var term = q.ToLowerInvariant();
        var users = await _um.Users
            .Include(u => u.Department)
            .Where(u => u.IsActive && (
                u.FullName.ToLower().Contains(term) ||
                u.Email!.ToLower().Contains(term) ||
                (u.Department != null && u.Department.Name.ToLower().Contains(term))))
            .OrderBy(u => u.FullName)
            .Take(8)
            .Select(u => new {
                id       = u.Id,
                name     = u.FullName,
                handle   = u.FullName.Replace(" ", "."),
                dept     = u.Department != null ? u.Department.Name : "",
                initials = u.FullName.Length > 0 ? u.FullName.Substring(0,1).ToUpper() : "?"
            })
            .ToListAsync();

        return Json(users);
    }

    [Authorize]
    public async Task<IActionResult> MyIssues()
    {
        var user = await _um.GetUserAsync(User);
        var submitted = await _svc.GetListAsync(new IssueFilterVm { PageSize=100 });
        submitted.Issues = submitted.Issues.Where(i => i.SubmittedById == user!.Id).ToList();
        var assigned = await _svc.GetListAsync(new IssueFilterVm { AssignedToId = user!.Id, PageSize=100 });
        ViewBag.Assigned = assigned.Issues;
        return View(submitted.Issues);
    }

    // ── Excel Export ──────────────────────────────────────────────────────────
    [Authorize]
    public async Task<IActionResult> Export(int? year, int? month)
    {
        var allIssues = await _svc.GetAllForExportAsync();

        if (year.HasValue && month.HasValue)
            allIssues = allIssues
                .Where(i => i.ReceivedDate.Year == year && i.ReceivedDate.Month == month)
                .ToList();

        using var wb = new XLWorkbook();

        // ── Summary ───────────────────────────────────────────────────────────
        var summary = wb.Worksheets.Add("Summary");
        var titleText = year.HasValue
            ? $"QCPC Issues — {new DateTime(year.Value, month!.Value, 1):MMMM yyyy}"
            : "QCPC Issues — All Time";

        summary.Cell("A1").Value = titleText;
        summary.Cell("A1").Style.Font.Bold = true;
        summary.Cell("A1").Style.Font.FontSize = 16;
        summary.Cell("A3").Value = "Generated:";    summary.Cell("B3").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        summary.Cell("A4").Value = "Total Issues:"; summary.Cell("B4").Value = allIssues.Count;
        summary.Cell("A5").Value = "Open:";         summary.Cell("B5").Value = allIssues.Count(i => i.Status == IssueStatus.Open);
        summary.Cell("A6").Value = "In Progress:";  summary.Cell("B6").Value = allIssues.Count(i => i.Status == IssueStatus.InProgress);
        summary.Cell("A7").Value = "Resolved:";     summary.Cell("B7").Value = allIssues.Count(i => i.Status == IssueStatus.Resolved);
        summary.Cell("A8").Value = "Critical:";     summary.Cell("B8").Value = allIssues.Count(i => i.Priority == IssuePriority.Critical);

        summary.Cell("D3").Value = "By Process"; summary.Cell("D3").Style.Font.Bold = true;
        int row = 4;
        foreach (var g in allIssues.GroupBy(i => i.Process).OrderBy(g => (int)g.Key))
        { summary.Cell(row,4).Value = FormatProcess(g.Key); summary.Cell(row,5).Value = g.Count(); row++; }

        summary.Cell("G3").Value = "By Priority"; summary.Cell("G3").Style.Font.Bold = true;
        row = 4;
        foreach (var g in allIssues.GroupBy(i => i.Priority).OrderBy(g => (int)g.Key))
        { summary.Cell(row,7).Value = g.Key.ToString(); summary.Cell(row,8).Value = g.Count(); row++; }
        summary.Columns().AdjustToContents();

        // ── Issues ────────────────────────────────────────────────────────────
        var ws = wb.Worksheets.Add("Issues");
        var headers = new[] {
            "Resp. No.","Title","Description","Type","Priority","Status","Process","Team",
            "Admitted","Resolved","Submitted By","Assigned To",
            "Received Date","Accepted Date","Promised Date","Completed Date",
            "Corrective Action","Tags","Answers","Comments"
        };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1,c+1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a3a5c");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        int r = 2;
        foreach (var i in allIssues.OrderBy(i => i.ReceivedDate))
        {
            ws.Cell(r,1).Value  = i.ResponseNumber;
            ws.Cell(r,2).Value  = i.Title;
            ws.Cell(r,3).Value  = i.Description;
            ws.Cell(r,4).Value  = i.Type.ToString();
            ws.Cell(r,5).Value  = i.Priority.ToString();
            ws.Cell(r,6).Value  = i.Status.ToString();
            ws.Cell(r,7).Value  = FormatProcess(i.Process);
            ws.Cell(r,8).Value  = i.ResponsibleTeam ?? "";
            ws.Cell(r,9).Value  = i.Admitted  ? "Yes" : "No";
            ws.Cell(r,10).Value = i.Resolved  ? "Yes" : "No";
            ws.Cell(r,11).Value = i.SubmittedBy?.FullName ?? "";
            ws.Cell(r,12).Value = i.AssignedTo?.FullName  ?? "";
            ws.Cell(r,13).Value = i.ReceivedDate.ToString("yyyy-MM-dd");
            ws.Cell(r,14).Value = i.AcceptedDate.ToString("yyyy-MM-dd");
            ws.Cell(r,15).Value = i.PromisedDate?.ToString("yyyy-MM-dd")  ?? "";
            ws.Cell(r,16).Value = i.CompletedDate?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(r,17).Value = i.CorrectiveAction ?? "";
            ws.Cell(r,18).Value = i.Tags;
            ws.Cell(r,19).Value = i.Answers.Count;
            ws.Cell(r,20).Value = i.Comments.Count;
            ws.Row(r).Style.Fill.BackgroundColor = i.Priority switch {
                IssuePriority.Critical => XLColor.FromHtml("#fde8e8"),
                IssuePriority.High     => XLColor.FromHtml("#fef3e0"),
                IssuePriority.Medium   => XLColor.FromHtml("#fefce8"),
                IssuePriority.Low      => XLColor.FromHtml("#ecfdf5"),
                _                      => r%2==0 ? XLColor.FromHtml("#f9fafb") : XLColor.White
            };
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.Column(2).Width = 40; ws.Column(3).Width = 60; ws.Column(17).Width = 50;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()!.SetAutoFilter();

        // ── Monthly Breakdown (full export only) ──────────────────────────────
        if (!year.HasValue)
        {
            var monthly = wb.Worksheets.Add("Monthly Breakdown");
            var mh = new[]{"Month","Total","Open","In Progress","Resolved","Critical"};
            for (int c = 0; c < mh.Length; c++) { monthly.Cell(1,c+1).Value = mh[c]; monthly.Cell(1,c+1).Style.Font.Bold=true; }
            int mr = 2;
            foreach (var g in allIssues.GroupBy(i => new{i.ReceivedDate.Year,i.ReceivedDate.Month}).OrderBy(g=>g.Key.Year).ThenBy(g=>g.Key.Month))
            {
                monthly.Cell(mr,1).Value = new DateTime(g.Key.Year,g.Key.Month,1).ToString("MMMM yyyy");
                monthly.Cell(mr,2).Value = g.Count();
                monthly.Cell(mr,3).Value = g.Count(i=>i.Status==IssueStatus.Open);
                monthly.Cell(mr,4).Value = g.Count(i=>i.Status==IssueStatus.InProgress);
                monthly.Cell(mr,5).Value = g.Count(i=>i.Status==IssueStatus.Resolved);
                monthly.Cell(mr,6).Value = g.Count(i=>i.Priority==IssuePriority.Critical);
                mr++;
            }
            monthly.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = year.HasValue
            ? $"QCPC_Issues_{year:D4}-{month:D2}.xlsx"
            : $"QCPC_Issues_All_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [Authorize]
    public async Task<IActionResult> Reports()
    {
        var all = await _svc.GetAllForExportAsync();
        // Use string arrays to avoid anonymous-type dynamic issues in Razor
        var months = all
            .GroupBy(i => new { i.ReceivedDate.Year, i.ReceivedDate.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g => new ReportMonthRow(
                g.Key.Year, g.Key.Month,
                new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                g.Count()))
            .ToList();
        ViewBag.Months     = months;
        ViewBag.TotalCount = all.Count;
        return View();
    }

    private static string FormatProcess(IssueProcess p) => p switch {
        IssueProcess.TestingAndQA       => "Testing & QA",
        IssueProcess.OperatingMonitoring=> "Operating & Monitoring",
        IssueProcess.TechnicalDocPrep   => "Technical Doc Prep",
        IssueProcess.CrossFunctional    => "Cross Functional",
        _ => p.ToString()
    };

    private static Issue MapToIssue(IssueFormVm vm) => new()
    {
        Title=vm.Title, Description=vm.Description, CorrectiveAction=vm.CorrectiveAction,
        Type=vm.Type, Priority=vm.Priority, Process=vm.Process, Status=vm.Status,
        QGroup=vm.QGroup, ResponsibleTeam=vm.ResponsibleTeam,
        AssignedToId=string.IsNullOrEmpty(vm.AssignedToId) ? null : vm.AssignedToId,
        PromisedDate=vm.PromisedDate, Tags=vm.Tags ?? "", Admitted=vm.Admitted, Resolved=vm.Resolved
    };

    private static IssueFormVm MapToVm(Issue i) => new()
    {
        Id=i.Id, Title=i.Title, Description=i.Description, CorrectiveAction=i.CorrectiveAction,
        Type=i.Type, Priority=i.Priority, Process=i.Process, Status=i.Status,
        QGroup=i.QGroup, ResponsibleTeam=i.ResponsibleTeam, AssignedToId=i.AssignedToId,
        PromisedDate=i.PromisedDate, Tags=i.Tags, Admitted=i.Admitted, Resolved=i.Resolved
    };

    private static void ApplyVm(IssueFormVm vm, Issue i)
    {
        i.Title=vm.Title; i.Description=vm.Description; i.CorrectiveAction=vm.CorrectiveAction;
        i.Type=vm.Type; i.Priority=vm.Priority; i.Process=vm.Process; i.Status=vm.Status;
        i.QGroup=vm.QGroup; i.ResponsibleTeam=vm.ResponsibleTeam;
        i.AssignedToId=string.IsNullOrEmpty(vm.AssignedToId) ? null : vm.AssignedToId;
        i.PromisedDate=vm.PromisedDate; i.Tags=vm.Tags ?? ""; i.Admitted=vm.Admitted; i.Resolved=vm.Resolved;
    }
}

// ── DTO used by Reports view (avoids anonymous type dynamic issues in Razor) ───
public record ReportMonthRow(int Year, int Month, string Label, int Count);
