using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QCPCMvc.Data;
using QCPCMvc.Models;
using QCPCMvc.Services;
using QCPCMvc.ViewModels;

namespace QCPCMvc.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly OtpService  _otp;
    private readonly EmailService _email;
    private readonly AppDbContext _db;

    public AccountController(
        UserManager<ApplicationUser> um,
        SignInManager<ApplicationUser> sm,
        OtpService otp,
        EmailService email,
        AppDbContext db)
    { _um = um; _sm = sm; _otp = otp; _email = email; _db = db; }

    // ── Login ─────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Allow login with either full email or username (without domain)
        var loginIdentifier = vm.Email;
        if (!vm.Email.Contains('@'))
        {
            // Username provided - append the default domain
            loginIdentifier = vm.Email.Trim().ToLowerInvariant() + "@ethiopianairlines.com";
        }

        var result = await _sm.PasswordSignInAsync(loginIdentifier, vm.Password, vm.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return LocalRedirect(string.IsNullOrEmpty(vm.ReturnUrl) ? "/" : vm.ReturnUrl);

        ModelState.AddModelError("", result.IsLockedOut
            ? "Account locked. Try again later."
            : "Invalid email or password.");
        return View(vm);
    }

    // ── Register Step 1 — collect details, send OTP ───────────────────────────
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        
        var departments = _db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToList();
        ViewBag.Departments = departments;
        return View(new RegisterVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        // Domain guard
        if (!string.IsNullOrEmpty(vm.Email) &&
            !vm.Email.ToLowerInvariant().EndsWith("@ethiopianairlines.com"))
        {
            ModelState.AddModelError(nameof(vm.Email),
                "Only @ethiopianairlines.com email addresses are permitted to register.");
        }

        if (!ModelState.IsValid) return View(vm);

        // Check email not already taken
        if (await _um.FindByEmailAsync(vm.Email) != null)
        {
            ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
            return View(vm);
        }

        // Store registration data in session-like TempData (survives one redirect)
        TempData["reg_fullname"]   = vm.FullName;
        TempData["reg_email"]      = vm.Email;
        TempData["reg_department"] = vm.DepartmentId.ToString();
        TempData["reg_password"]   = vm.Password; // held briefly, discarded after OTP verify

        // Send OTP
        var sent = await _otp.SendOtpAsync(vm.Email, vm.FullName);
        if (!sent)
        {
            ModelState.AddModelError("", "Too many verification attempts. Please wait 1 hour before trying again.");
            return View(vm);
        }

        TempData["Success"] = $"A 6-digit verification code was sent to {vm.Email}. Enter it below.";
        return RedirectToAction(nameof(VerifyEmail), new { email = vm.Email });
    }

    // ── Register Step 2 — verify OTP, create account ─────────────────────────
    [HttpGet]
    public IActionResult VerifyEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Register));
        return View(new VerifyOtpVm { Email = email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyOtpVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _otp.VerifyAsync(vm.Email, vm.Code.Trim());

        if (result == OtpVerifyResult.Expired)
        {
            ModelState.AddModelError(nameof(vm.Code), "Code has expired. Please register again to get a new code.");
            return View(vm);
        }
        if (result != OtpVerifyResult.Success)
        {
            ModelState.AddModelError(nameof(vm.Code), "Invalid code. Please check and try again.");
            return View(vm);
        }

        // Retrieve registration data from TempData
        var fullName   = TempData["reg_fullname"]   as string ?? "";
        var email      = TempData["reg_email"]      as string ?? vm.Email;
        var deptIdStr  = TempData["reg_department"] as string ?? "";
        var password   = TempData["reg_password"]   as string ?? "";

        if (string.IsNullOrEmpty(password))
        {
            // TempData expired (e.g. session timeout) — send them back
            TempData["Error"] = "Session expired. Please register again.";
            return RedirectToAction(nameof(Register));
        }

        // Parse department ID
        int? departmentId = null;
        if (int.TryParse(deptIdStr, out var parsedId))
        {
            departmentId = parsedId;
        }

        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            FullName       = fullName,
            DepartmentId   = departmentId,
            EmailConfirmed = true,  // verified by OTP
        };

        var createResult = await _um.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        await _um.AddToRoleAsync(user, "Developer");
        await _sm.SignInAsync(user, isPersistent: false);

        TempData.Clear();
        TempData["Success"] = "Email verified! Welcome to QCPC Issue Tracker.";
        return RedirectToAction("Index", "Home");
    }

    // ── Resend OTP ────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        // Restore fullname from TempData — keep it alive for the retry
        var fullName = TempData.Peek("reg_fullname") as string ?? email.Split('@')[0];
        // Keep other TempData alive
        TempData.Keep();

        var sent = await _otp.SendOtpAsync(email, fullName);
        TempData["Success"] = sent
            ? $"A new code was sent to {email}."
            : "Too many attempts. Please wait a while before requesting another code.";

        return RedirectToAction(nameof(VerifyEmail), new { email });
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _sm.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ── Forgot Password ─────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new ForgotPasswordVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _um.FindByEmailAsync(vm.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(vm.Email), "No account found with this email address.");
            return View(vm);
        }

        // Generate password reset token
        var token = await _um.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Action("ResetPassword", "Account", new { email = user.Email, token = token }, Request.Scheme);

        // Send reset email
        await _email.SendPasswordResetEmailAsync(user.Email, user.FullName, resetUrl);

        // Show confirmation page
        return View("ForgotPasswordConfirmation", vm);
    }

    // ── Reset Password ──────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Invalid password reset link.";
            return RedirectToAction(nameof(Login));
        }
        return View(new ResetPasswordVm { Email = email, Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _um.FindByEmailAsync(vm.Email);
        if (user == null)
        {
            TempData["Error"] = "Invalid password reset link.";
            return RedirectToAction(nameof(Login));
        }

        var result = await _um.ResetPasswordAsync(user, vm.Token, vm.NewPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Your password has been reset successfully. You can now sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        return View(vm);
    }

    // ── Manage Profile (View/Edit) ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Manage()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        
        var user = await _um.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        var departments = _db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToList();
        ViewBag.Departments = departments;
        
        var vm = new ManageProfileVm
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            DepartmentId = user.DepartmentId
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Manage(ManageProfileVm vm)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        
        var user = await _um.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            var departments = _db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToList();
            ViewBag.Departments = departments;
            return View(vm);
        }

        // Update profile
        user.FullName = vm.FullName;
        user.DepartmentId = vm.DepartmentId;

        var result = await _um.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Manage));
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        
        var departments2 = _db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToList();
        ViewBag.Departments = departments2;
        return View(vm);
    }

    // ── Change Password ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        return View(new ChangePasswordVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        
        if (!ModelState.IsValid) return View(vm);

        var user = await _um.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        var result = await _um.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Your password has been changed successfully.";
            await _sm.RefreshSignInAsync(user); // Keep user logged in
            return RedirectToAction(nameof(Manage));
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        return View(vm);
    }

    // ── Delete Account ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult DeleteAccount()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        return View(new DeleteAccountVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountVm vm)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        
        if (!ModelState.IsValid) return View(vm);

        var user = await _um.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        // Verify password before deletion
        var isPasswordValid = await _um.CheckPasswordAsync(user, vm.Password);
        if (!isPasswordValid)
        {
            ModelState.AddModelError(nameof(vm.Password), "Incorrect password.");
            return View(vm);
        }

        var result = await _um.DeleteAsync(user);
        if (result.Succeeded)
        {
            await _sm.SignOutAsync();
            TempData["Success"] = "Your account has been deleted successfully.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        return View(vm);
    }
}
