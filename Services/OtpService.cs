using Microsoft.EntityFrameworkCore;
using QCPCMvc.Data;
using QCPCMvc.Models;

namespace QCPCMvc.Services;

public class OtpService
{
    private readonly AppDbContext  _db;
    private readonly EmailService  _email;
    private const int ExpiryMinutes = 10;
    private const int MaxAttemptsPerHour = 5;

    public OtpService(AppDbContext db, EmailService email)
    { _db = db; _email = email; }

    /// <summary>
    /// Generates a 6-digit OTP, persists it, and sends the email.
    /// Returns false if the email has been rate-limited.
    /// </summary>
    public async Task<bool> SendOtpAsync(string email, string displayName)
    {
        // Rate-limit: max 5 OTPs per email per hour
        var cutoff   = DateTime.UtcNow.AddHours(-1);
        var recentCount = await _db.EmailOtps
            .CountAsync(o => o.Email == email && o.CreatedAt > cutoff);

        if (recentCount >= MaxAttemptsPerHour)
            return false;

        // Expire any existing unused OTPs for this email
        await _db.EmailOtps
            .Where(o => o.Email == email && !o.Used)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Used, true));

        var code = GenerateCode();
        _db.EmailOtps.Add(new EmailOtp
        {
            Email     = email,
            Code      = BCrypt(code),   // store hashed
            ExpiresAt = DateTime.UtcNow.AddMinutes(ExpiryMinutes),
        });
        await _db.SaveChangesAsync();

        await _email.SendOtpEmailAsync(email, displayName, code, ExpiryMinutes);
        return true;
    }

    /// <summary>Validates the code entered by the user. Marks it used if correct.</summary>
    public async Task<OtpVerifyResult> VerifyAsync(string email, string code)
    {
        var otp = await _db.EmailOtps
            .Where(o => o.Email == email && !o.Used)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return OtpVerifyResult.NotFound;

        if (otp.ExpiresAt < DateTime.UtcNow)
        {
            otp.Used = true;
            await _db.SaveChangesAsync();
            return OtpVerifyResult.Expired;
        }

        if (!VerifyBCrypt(code, otp.Code))
            return OtpVerifyResult.Invalid;

        otp.Used = true;
        await _db.SaveChangesAsync();
        return OtpVerifyResult.Success;
    }

    public async Task<bool> HasValidOtpAsync(string email) =>
        await _db.EmailOtps.AnyAsync(o =>
            o.Email == email && !o.Used && o.ExpiresAt > DateTime.UtcNow);

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string GenerateCode() =>
        Random.Shared.Next(100_000, 999_999).ToString();

    // Simple PBKDF2 hash to avoid storing plain OTP in DB
    private static string BCrypt(string code)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(code + "qcpc-otp-salt");
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static bool VerifyBCrypt(string code, string hash) =>
        BCrypt(code) == hash;
}

public enum OtpVerifyResult { Success, NotFound, Expired, Invalid }
