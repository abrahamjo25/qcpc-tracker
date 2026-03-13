using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace QCPCMvc.Models;

public class ApplicationUser : IdentityUser
{
    [Required] public string FullName   { get; set; } = "";
    public int?    DepartmentId         { get; set; }
    public Department? Department        { get; set; }
    public string? AvatarInitials       { get; set; }
    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
    public bool IsActive                { get; set; } = true;

    public ICollection<Issue>        SubmittedIssues { get; set; } = new List<Issue>();
    public ICollection<IssueAnswer>  Answers         { get; set; } = new List<IssueAnswer>();
    public ICollection<IssueComment> Comments        { get; set; } = new List<IssueComment>();
    public ICollection<Notification> Notifications   { get; set; } = new List<Notification>();
}

/// <summary>Department with group email for team notifications.</summary>
public class Department
{
    public int      Id         { get; set; }
    [Required] public string Name      { get; set; } = "";
    public string   GroupEmail { get; set; } = "";
    public bool     IsActive   { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}

/// <summary>6-digit OTP record for email verification during registration.</summary>
public class EmailOtp
{
    public int      Id        { get; set; }
    public string   Email     { get; set; } = "";
    public string   Code      { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool     Used      { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Stores the OpenAI embedding vector for an issue (for semantic / vector search).</summary>
public class IssueEmbedding
{
    public int    IssueId    { get; set; }
    public Issue  Issue      { get; set; } = null!;
    /// <summary>JSON-serialised float[] — the 1536-dim text-embedding-3-small vector.</summary>
    public string VectorJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Issue
{
    public int    Id             { get; set; }
    public int    ResponseNumber { get; set; }
    [Required] public string Title       { get; set; } = "";
    [Required] public string Description { get; set; } = "";

    public IssueType     Type      { get; set; } = IssueType.Internal;
    public IssuePriority Priority  { get; set; } = IssuePriority.Medium;
    public IssueProcess  Process   { get; set; } = IssueProcess.Implementation;
    public IssueStatus   Status    { get; set; } = IssueStatus.Open;
    public QualityGroup  QGroup    { get; set; } = QualityGroup.Group1;

    public string? CorrectiveAction  { get; set; }
    public string? ResponsibleTeam   { get; set; }
    public bool    Admitted          { get; set; }
    public bool    Resolved          { get; set; }
    public string  Tags              { get; set; } = "";

    public DateTime  ReceivedDate  { get; set; } = DateTime.UtcNow;
    public DateTime  AcceptedDate  { get; set; } = DateTime.UtcNow;
    public DateTime? PromisedDate  { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime  UpdatedAt     { get; set; } = DateTime.UtcNow;

    public string           SubmittedById { get; set; } = "";
    public ApplicationUser  SubmittedBy   { get; set; } = null!;
    public string?          AssignedToId  { get; set; }
    public ApplicationUser? AssignedTo    { get; set; }

    public ICollection<IssueAnswer>   Answers    { get; set; } = new List<IssueAnswer>();
    public ICollection<IssueComment>  Comments   { get; set; } = new List<IssueComment>();
    public ICollection<IssueActivity> Activities { get; set; } = new List<IssueActivity>();
}

public class IssueAnswer
{
    public int    Id         { get; set; }
    public int    IssueId    { get; set; }
    public Issue  Issue      { get; set; } = null!;
    public string Body       { get; set; } = "";
    public bool   IsAccepted { get; set; }
    public int    Upvotes    { get; set; }
    public string          AuthorId  { get; set; } = "";
    public ApplicationUser Author    { get; set; } = null!;
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt        { get; set; } = DateTime.UtcNow;
    public ICollection<IssueComment> Comments { get; set; } = new List<IssueComment>();
}

public class IssueComment
{
    public int    Id      { get; set; }
    public int?   IssueId { get; set; }
    public Issue? Issue   { get; set; }
    public int?   AnswerId{ get; set; }
    public IssueAnswer? Answer { get; set; }
    public int?   ParentId{ get; set; }
    public IssueComment? Parent  { get; set; }
    public ICollection<IssueComment> Replies { get; set; } = new List<IssueComment>();
    public string Body    { get; set; } = "";
    public bool   IsEdited{ get; set; }
    public string          AuthorId  { get; set; } = "";
    public ApplicationUser Author    { get; set; } = null!;
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
}

public class IssueActivity
{
    public int    Id      { get; set; }
    public int    IssueId { get; set; }
    public Issue  Issue   { get; set; } = null!;
    public string ActorId { get; set; } = "";
    public ApplicationUser Actor { get; set; } = null!;
    public ActivityType  Type        { get; set; }
    public string        Description { get; set; } = "";
    public DateTime      CreatedAt   { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int    Id      { get; set; }
    public string UserId  { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public NotificationType Type    { get; set; }
    public string Title   { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Link   { get; set; }
    public bool   IsRead  { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Enumerations ───────────────────────────────────────────────────────────────
public enum IssueType    { Internal, External }
public enum IssuePriority{ Critical = 1, High = 2, Medium = 3, Low = 4, Minimal = 5 }
public enum IssueProcess
{
    Planning = 1, Designing = 2, Implementation = 3,
    TestingAndQA = 4, Deploying = 5, OperatingMonitoring = 6,
    TechnicalDocPrep = 7, EHS = 8, CrossFunctional = 9
}
public enum IssueStatus { Open, InProgress, PendingReview, Resolved, Closed, Rejected }
public enum QualityGroup { Group1 = 1, Group2 = 2, Group3 = 3 }
public enum ActivityType
{
    Created, StatusChanged, Assigned, AnswerAdded,
    CommentAdded, AnswerAccepted, Resolved, Closed, Edited
}
public enum NotificationType
{
    NewIssue, IssueAssigned, IssueResolved, NewAnswer,
    AnswerAccepted, NewComment, Mentioned, IssueUpdated
}
