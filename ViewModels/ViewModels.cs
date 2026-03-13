using System.ComponentModel.DataAnnotations;
using QCPCMvc.Models;
using QCPCMvc.Validation;

namespace QCPCMvc.ViewModels;

// ─── Auth ─────────────────────────────────────────────────────────────────────
public class LoginVm
{
    [Required] public string Email    { get; set; } = "";
    [Required]               public string Password { get; set; } = "";
    public bool    RememberMe { get; set; }
    public string? ReturnUrl  { get; set; }
}

public class RegisterVm
{
    [Required, Display(Name="Full Name"), StringLength(100, MinimumLength=2)]
    public string FullName   { get; set; } = "";

    [Required, EmailAddress]
    [AllowedEmailDomain("ethiopianairlines.com")]
    public string Email      { get; set; } = "";

    [Required]
    [Display(Name="Department")]
    public int DepartmentId { get; set; }

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password   { get; set; } = "";

    [Required, DataType(DataType.Password), Compare("Password", ErrorMessage="Passwords do not match.")]
    [Display(Name="Confirm Password")]
    public string Confirm    { get; set; } = "";
}

/// <summary>OTP verification step shown after Register form submission.</summary>
public class VerifyOtpVm
{
    /// <summary>Email that was registered — carried through to bind OTP to correct address.</summary>
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    /// <summary>The 6-digit code the user types in.</summary>
    [Required, StringLength(6, MinimumLength=6, ErrorMessage="Enter the 6-digit code.")]
    [Display(Name="Verification Code")]
    public string Code  { get; set; } = "";
}

/// <summary>Forgot password - enter email to receive reset link.</summary>
public class ForgotPasswordVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

/// <summary>Reset password - set new password after clicking reset link.</summary>
public class ResetPasswordVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name="New Password")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage="Passwords do not match.")]
    [Display(Name="Confirm Password")]
    public string ConfirmPassword { get; set; } = "";
}

// ─── Issue ────────────────────────────────────────────────────────────────────
public class IssueFormVm
{
    public int Id { get; set; }

    [Required, StringLength(200, MinimumLength=5)]
    public string Title { get; set; } = "";

    [Required, StringLength(5000, MinimumLength=10)]
    public string Description { get; set; } = "";

    public string?       CorrectiveAction { get; set; }
    public IssueType     Type             { get; set; } = IssueType.Internal;
    public IssuePriority Priority         { get; set; } = IssuePriority.Medium;
    public IssueProcess  Process          { get; set; } = IssueProcess.Implementation;
    public IssueStatus   Status           { get; set; } = IssueStatus.Open;
    public QualityGroup  QGroup           { get; set; } = QualityGroup.Group1;
    public string?       ResponsibleTeam  { get; set; }
    public string?       AssignedToId     { get; set; }

    [DataType(DataType.Date)]
    public DateTime?     PromisedDate     { get; set; }
    public string?       Tags             { get; set; }
    public bool          Admitted         { get; set; }
    public bool          Resolved         { get; set; }
}

public class IssueFilterVm
{
    public string?        Search           { get; set; }
    public IssueStatus?   Status           { get; set; }
    public IssuePriority? Priority         { get; set; }
    public IssueProcess?  Process          { get; set; }
    public IssueType?     Type             { get; set; }
    public string?        AssignedToId     { get; set; }
    public int            Page             { get; set; } = 1;
    public int            PageSize         { get; set; } = 20;
    /// <summary>Set by IssueService when results come from vector search.</summary>
    public bool           VectorSearchActive { get; set; }
}

public class IssueListVm
{
    public List<Issue>   Issues     { get; set; } = new();
    public IssueFilterVm Filter     { get; set; } = new();
    public int           TotalCount { get; set; }
    public int           TotalPages => (int)Math.Ceiling((double)TotalCount / Filter.PageSize);
}

public class IssueDetailVm
{
    public Issue               Issue      { get; set; } = null!;
    public List<ApplicationUser> Users    { get; set; } = new();
    public string              NewAnswer  { get; set; } = "";
    public string              NewComment { get; set; } = "";
}

// ─── Dashboard ────────────────────────────────────────────────────────────────
public class DashboardVm
{
    public int Total        { get; set; }
    public int Open         { get; set; }
    public int InProgress   { get; set; }
    public int Resolved     { get; set; }
    public int Critical     { get; set; }
    public int OverdueCount { get; set; }
    public List<Issue> RecentIssues { get; set; } = new();
    public Dictionary<IssueProcess,  int> ByProcess  { get; set; } = new();
    public Dictionary<IssuePriority, int> ByPriority { get; set; } = new();
    public Dictionary<string, int>        ByTeam     { get; set; } = new();
}

// ─── Reports ──────────────────────────────────────────────────────────────────
public class MonthSummaryVm
{
    public int    Year  { get; set; }
    public int    Month { get; set; }
    public int    Count { get; set; }
    public string Label { get; set; } = "";
}

// ─── Account Management ──────────────────────────────────────────────────────
public class ManageProfileVm
{
    [Required, Display(Name="Full Name"), StringLength(100, MinimumLength=2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Display(Name="Department")]
    public int? DepartmentId { get; set; }
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password)]
    [Display(Name="Current Password")]
    public string CurrentPassword { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name="New Password")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage="Passwords do not match.")]
    [Display(Name="Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}

public class DeleteAccountVm
{
    [Required, DataType(DataType.Password)]
    [Display(Name="Enter your password to confirm account deletion")]
    public string Password { get; set; } = "";
}
