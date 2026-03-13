using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QCPCMvc.Data;
using QCPCMvc.Models;
using QCPCMvc.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                   sql => sql.EnableRetryOnFailure()));

// ── HTTP Context Accessor ───────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequireDigit           = true;
        o.Password.RequireLowercase       = true;
        o.Password.RequireUppercase       = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequiredLength         = 8;
        // Email confirmed via OTP — ASP.NET token emails are disabled
        o.SignIn.RequireConfirmedEmail     = false;
        o.User.RequireUniqueEmail         = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath         = "/Account/Login";
    o.LogoutPath        = "/Account/Logout";
    o.AccessDeniedPath  = "/Account/Login";
    o.ExpireTimeSpan    = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
    o.Cookie.HttpOnly   = true;
    o.Cookie.SameSite   = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

// ── Authorization policies ────────────────────────────────────────────────────
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admin",   p => p.RequireRole("Admin"));
    o.AddPolicy("Manager", p => p.RequireRole("Admin", "QualityManager"));
});

// ── Email service ─────────────────────────────────────────────────────────────
var emailSettings = builder.Configuration.GetSection("Email").Get<EmailSettings>()
                    ?? new EmailSettings();

// Log email configuration at startup (mask password for security)
Console.WriteLine($"[Email Config] Enabled:{emailSettings.Enabled}, Host:{emailSettings.Host}, Port:{emailSettings.Port}, UseSsl:{emailSettings.UseSsl}, From:{emailSettings.FromAddress}");

builder.Services.AddSingleton(emailSettings);
builder.Services.AddScoped<EmailService>();

// ── OTP service ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<OtpService>();

// ── HTTP client (used by VectorSearchService for OpenAI API calls) ────────────
builder.Services.AddHttpClient();

// ── Vector search service ─────────────────────────────────────────────────────
builder.Services.AddScoped<VectorSearchService>();

// ── Issue service ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IssueService>();

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews(o =>
        o.Filters.Add<QCPCMvc.Filters.NotificationCountFilter>())
    .AddRazorRuntimeCompilation();

// ── Session (used to carry registration data across OTP redirect) ─────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout        = TimeSpan.FromMinutes(20);
    o.Cookie.HttpOnly    = true;
    o.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();           // must come before UseAuthentication
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

await DbSeeder.SeedAsync(app.Services);
app.Run();
