using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QCPCMvc.Data;
using QCPCMvc.Models;
using QCPCMvc.ViewModels;
using System.Text.RegularExpressions;

namespace QCPCMvc.Services;

public class IssueService
{
    private readonly AppDbContext  _db;
    private readonly EmailService  _email;
    private readonly VectorSearchService _vectors;
    private readonly UserManager<ApplicationUser> _um;
    private readonly IHttpContextAccessor _httpContext;

    public IssueService(
        AppDbContext db, EmailService email,
        VectorSearchService vectors, UserManager<ApplicationUser> um,
        IHttpContextAccessor httpContext)
    { 
        _db = db; 
        _email = email; 
        _vectors = vectors; 
        _um = um; 
        _httpContext = httpContext;
    }

    // Helper to get base URL dynamically from current request
    private string GetBaseUrl()
    {
        var request = _httpContext.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "http://10.0.227.152:7895"; // fallback
    }

    private IQueryable<Issue> Base() =>
        _db.Issues
           .Include(i => i.SubmittedBy).Include(i => i.AssignedTo)
           .Include(i => i.Answers).ThenInclude(a => a.Author)
           .Include(i => i.Comments).ThenInclude(c => c.Author);

    // ── List with optional vector search ─────────────────────────────────────
    public async Task<IssueListVm> GetListAsync(IssueFilterVm f)
    {
        List<int>? vectorIds = null;
        if (!string.IsNullOrWhiteSpace(f.Search))
            vectorIds = await _vectors.SearchAsync(f.Search);

        var q = Base();

        if (vectorIds != null && vectorIds.Any())
        {
            q = q.Where(i => vectorIds.Contains(i.Id));
            f.VectorSearchActive = true;
        }
        else if (!string.IsNullOrWhiteSpace(f.Search))
        {
            q = q.Where(i => i.Title.Contains(f.Search)
                           || i.Description.Contains(f.Search)
                           || i.Tags.Contains(f.Search));
        }

        if (f.Status   != null) q = q.Where(i => i.Status   == f.Status);
        if (f.Priority != null) q = q.Where(i => i.Priority == f.Priority);
        if (f.Process  != null) q = q.Where(i => i.Process  == f.Process);
        if (f.Type     != null) q = q.Where(i => i.Type     == f.Type);
        if (!string.IsNullOrEmpty(f.AssignedToId)) q = q.Where(i => i.AssignedToId == f.AssignedToId);

        var total = await q.CountAsync();
        List<Issue> items;

        if (vectorIds != null && vectorIds.Any())
        {
            var all = await q.ToListAsync();
            items = vectorIds
                .Select(id => all.FirstOrDefault(i => i.Id == id))
                .Where(i => i != null).Cast<Issue>()
                .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();
        }
        else
        {
            items = await q.OrderByDescending(i => i.ReceivedDate)
                           .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync();
        }

        return new IssueListVm { Issues = items, Filter = f, TotalCount = total };
    }

    public async Task<Issue?> GetDetailAsync(int id) =>
        await _db.Issues
            .Include(i => i.SubmittedBy).Include(i => i.AssignedTo)
            .Include(i => i.Answers).ThenInclude(a => a.Author)
            .Include(i => i.Answers).ThenInclude(a => a.Comments).ThenInclude(c => c.Author)
            .Include(i => i.Comments).ThenInclude(c => c.Author)
            .Include(i => i.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Author)
            .Include(i => i.Activities).ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Issue> CreateAsync(Issue issue, string actorId)
    {
        issue.ResponseNumber = (await _db.Issues.MaxAsync(i => (int?)i.ResponseNumber) ?? 0) + 1;
        issue.ReceivedDate = issue.UpdatedAt = DateTime.UtcNow;
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync();
        await LogAsync(issue.Id, actorId, ActivityType.Created, $"Issue #{issue.ResponseNumber} created");
        await NotifyAsync(issue.AssignedToId, NotificationType.IssueAssigned,
            "Issue Assigned", $"You were assigned: {issue.Title}", $"/Issues/Detail/{issue.Id}");
        
        // Notify department group email
        await NotifyDepartmentGroupAsync(issue, actorId);
        
        _ = Task.Run(() => _vectors.IndexIssueAsync(issue.Id));
        return issue;
    }

    private async Task NotifyDepartmentGroupAsync(Issue issue, string actorId)
    {
        try
        {
            // Get the submitter's department
            var submitter = await _um.FindByIdAsync(actorId);
            if (submitter?.DepartmentId == null) return;

            var department = await _db.Departments.FindAsync(submitter.DepartmentId);
            if (department == null || string.IsNullOrEmpty(department.GroupEmail)) return;

            // Get base URL for the issue link dynamically
            var baseUrl = GetBaseUrl();
            
            // Send email to department group
            _ = Task.Run(() => _email.SendNewIssueNotificationToGroupAsync(
                department.GroupEmail,
                department.Name,
                submitter.FullName,
                issue.Title,
                issue.Id,
                baseUrl));
        }
        catch (Exception)
        {
            // Don't fail the issue creation if email fails
        }
    }

    public async Task UpdateAsync(Issue issue, string actorId)
    {
        issue.UpdatedAt = DateTime.UtcNow;
        _db.Issues.Update(issue);
        await _db.SaveChangesAsync();
        await LogAsync(issue.Id, actorId, ActivityType.Edited, "Issue updated");
        _ = Task.Run(() => _vectors.IndexIssueAsync(issue.Id));
    }

    public async Task ChangeStatusAsync(int id, IssueStatus s, string actorId)
    {
        var issue = await _db.Issues.FindAsync(id);
        if (issue == null) return;
        var old = issue.Status;
        issue.Status = s; issue.UpdatedAt = DateTime.UtcNow;
        if (s == IssueStatus.Resolved) { issue.Resolved = true; issue.CompletedDate = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        await LogAsync(id, actorId, ActivityType.StatusChanged, $"Status: {old} → {s}");
        if (issue.SubmittedById != actorId)
            await NotifyAsync(issue.SubmittedById, NotificationType.IssueUpdated,
                "Issue Updated", $"Status changed to {s}: {issue.Title}", $"/Issues/Detail/{id}");
    }

    public async Task AssignAsync(int id, string? assigneeId, string actorId)
    {
        var issue = await _db.Issues.FindAsync(id);
        if (issue == null) return;
        issue.AssignedToId = assigneeId; issue.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAsync(id, actorId, ActivityType.Assigned, assigneeId == null ? "Unassigned" : $"Assigned to {assigneeId}");
        if (assigneeId != null)
            await NotifyAsync(assigneeId, NotificationType.IssueAssigned,
                "Issue Assigned", "You were assigned an issue", $"/Issues/Detail/{id}");
    }

    // ── Answer: parse @mentions, send email + in-app notification ────────────
    public async Task<IssueAnswer> AddAnswerAsync(
        int issueId, string body, string authorId, string? baseUrl = null)
    {
        var ans = new IssueAnswer { IssueId = issueId, Body = body, AuthorId = authorId };
        _db.IssueAnswers.Add(ans);
        await _db.SaveChangesAsync();
        await LogAsync(issueId, authorId, ActivityType.AnswerAdded, "Answer posted");

        var issue = await _db.Issues.FindAsync(issueId);
        if (issue?.SubmittedById != authorId)
            await NotifyAsync(issue?.SubmittedById, NotificationType.NewAnswer,
                "New Answer", "Someone answered your issue", $"/Issues/Detail/{issueId}");

        if (issue != null && !string.IsNullOrEmpty(baseUrl))
        {
            var author = await _um.FindByIdAsync(authorId);
            if (author != null)
                await ProcessMentionsAsync(body, authorId, author.FullName, issue, baseUrl);
        }
        return ans;
    }

    public async Task AcceptAnswerAsync(int answerId, string actorId)
    {
        var ans = await _db.IssueAnswers.Include(a => a.Issue).ThenInclude(i => i.Answers)
                                        .FirstOrDefaultAsync(a => a.Id == answerId);
        if (ans == null) return;
        foreach (var a in ans.Issue.Answers) a.IsAccepted = false;
        ans.IsAccepted = true;
        await _db.SaveChangesAsync();
        await LogAsync(ans.IssueId, actorId, ActivityType.AnswerAccepted, "Answer accepted");
        if (ans.AuthorId != actorId)
            await NotifyAsync(ans.AuthorId, NotificationType.AnswerAccepted,
                "Answer Accepted!", "Your answer was marked as accepted", $"/Issues/Detail/{ans.IssueId}");
    }

    // ── Comment: parse @mentions, send email + in-app notification ───────────
    public async Task AddCommentAsync(
        int? issueId, int? answerId, string body,
        string authorId, int? parentId = null, string? baseUrl = null)
    {
        var c = new IssueComment
        {
            IssueId=issueId, AnswerId=answerId, Body=body,
            AuthorId=authorId, ParentId=parentId
        };
        _db.IssueComments.Add(c);
        await _db.SaveChangesAsync();
        if (issueId.HasValue)
            await LogAsync(issueId.Value, authorId, ActivityType.CommentAdded, "Comment added");

        if (issueId.HasValue && !string.IsNullOrEmpty(baseUrl))
        {
            var issue  = await _db.Issues.FindAsync(issueId.Value);
            var author = await _um.FindByIdAsync(authorId);
            if (issue != null && author != null)
                await ProcessMentionsAsync(body, authorId, author.FullName, issue, baseUrl);
        }
    }

    public async Task<DashboardVm> GetDashboardAsync()
    {
        var all    = await _db.Issues.Include(i => i.SubmittedBy).ToListAsync();
        var recent = await Base().OrderByDescending(i => i.ReceivedDate).Take(8).ToListAsync();
        return new DashboardVm
        {
            Total        = all.Count,
            Open         = all.Count(i => i.Status == IssueStatus.Open),
            InProgress   = all.Count(i => i.Status == IssueStatus.InProgress),
            Resolved     = all.Count(i => i.Status == IssueStatus.Resolved),
            Critical     = all.Count(i => i.Priority == IssuePriority.Critical && i.Status != IssueStatus.Resolved),
            OverdueCount = all.Count(i => i.PromisedDate < DateTime.UtcNow && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed),
            RecentIssues = recent,
            ByProcess    = all.GroupBy(i => i.Process).ToDictionary(g => g.Key, g => g.Count()),
            ByPriority   = all.GroupBy(i => i.Priority).ToDictionary(g => g.Key, g => g.Count()),
            ByTeam       = all.Where(i => i.ResponsibleTeam != null)
                              .GroupBy(i => i.ResponsibleTeam!)
                              .ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    public async Task<List<Notification>> GetNotificationsAsync(string userId, bool unreadOnly = false)
    {
        var q = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        return await q.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
    }

    public async Task<int>  GetUnreadCountAsync(string userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkNotifReadAsync(int id)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); }
    }

    public async Task MarkAllReadAsync(string userId) =>
        await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
                                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public async Task<List<Issue>> GetAllForExportAsync() =>
        await _db.Issues
            .Include(i => i.SubmittedBy).Include(i => i.AssignedTo)
            .Include(i => i.Answers).Include(i => i.Comments)
            .OrderBy(i => i.ReceivedDate).ToListAsync();

    // ── @Mention parsing ──────────────────────────────────────────────────────
    /// <summary>
    /// Scans body text for @Firstname.Lastname patterns, looks up the user,
    /// sends an email notification AND an in-app notification.
    /// Deduplicates so the same user is not notified twice per save.
    /// </summary>
    private async Task ProcessMentionsAsync(
        string body, string authorId, string authorName,
        Issue issue, string baseUrl)
    {
        // Match @Word or @Word.Word  (handles single names and firstname.lastname)
        var matches = Regex.Matches(body, @"@([\w][\w.]{1,50})", RegexOptions.IgnoreCase);
        var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in matches)
        {
            var handle = m.Groups[1].Value.ToLowerInvariant(); // e.g. "solomon.melaku"
            // Find user whose FullName (lowercased, spaces→dots) matches handle
            var users = await _um.Users.ToListAsync();
            var target = users.FirstOrDefault(u =>
                u.FullName.ToLowerInvariant().Replace(" ", ".") == handle
                || u.FullName.ToLowerInvariant().Replace(" ", "") == handle.Replace(".", "")
                || (u.Email != null && u.Email.Split('@')[0].ToLowerInvariant() == handle));

            if (target == null || target.Id == authorId) continue;
            if (!notified.Add(target.Id)) continue; // already notified this save

            // In-app notification
            await NotifyAsync(target.Id, NotificationType.Mentioned,
                $"@{authorName} mentioned you",
                $"\"{issue.Title}\" — please participate",
                $"/Issues/Detail/{issue.Id}");

            // Email notification (fire-and-forget, never crash the request)
            var url = baseUrl.TrimEnd('/');
            _ = Task.Run(() => _email.SendMentionEmailAsync(
                target.Email ?? "", target.FullName,
                authorName, issue.Title, issue.Id, url));
        }
    }

    private async Task LogAsync(int issueId, string actorId, ActivityType t, string desc)
    {
        _db.IssueActivities.Add(new IssueActivity { IssueId=issueId, ActorId=actorId, Type=t, Description=desc });
        await _db.SaveChangesAsync();
    }

    private async Task NotifyAsync(string? userId, NotificationType t, string title, string msg, string? link)
    {
        if (string.IsNullOrEmpty(userId)) return;
        _db.Notifications.Add(new Notification { UserId=userId, Type=t, Title=title, Message=msg, Link=link });
        await _db.SaveChangesAsync();
    }
}
