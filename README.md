# QCPC Issue Tracker — ASP.NET Core MVC (.NET 8)

## Quick Start

```bash
cd QCPCMvc
dotnet restore
dotnet run
# Open https://localhost:5001
# DB auto-created and seeded on first run
```

## Default Credentials (seeded)

| Email | Password | Role |
|---|---|---|
| admin@qcpc.local | Admin@1234! | Admin |
| solomon.melaku@qcpc.local | User@1234! | Developer |
| (other users) | User@1234! | Developer |

> Seeded users bypass OTP — OTP applies only to new registrations.

---

## Feature: Email Notifications on @Mention

When a user types `@Firstname.Lastname` in a comment or answer body, the system:
1. Resolves the user by matching `FullName` (spaces replaced with `.`) against the handle
2. Creates an in-app notification
3. Sends an HTML email with subject:
   **"[QCPC] Abraham mentioned you in «Issue Title»"**

The email body explains who mentioned them, shows the issue title, and links directly to the issue.

### Enable email sending

Edit `appsettings.json`:
```json
"Email": {
  "Enabled": true,
  "Host": "smtp.gmail.com",
  "Port": 587,
  "UseSsl": false,
  "UserName": "yourapp@gmail.com",
  "Password": "your-app-password",
  "FromAddress": "yourapp@gmail.com",
  "FromName": "QCPC Issue Tracker"
}
```

For Gmail: create an **App Password** (Settings → Security → 2-Step Verification → App Passwords).

When `Enabled: false` (default), email calls are logged but no SMTP connection is made — safe for development.

---

## Feature: OTP Email Verification on Register

New registrations go through a 2-step flow:
1. **Register** — fill in name, email, password → a 6-digit OTP is emailed
2. **Verify** — enter the code in the 6-box digit UI → account created

Security details:
- OTP hashed with SHA-256 before DB storage
- Expires in 10 minutes
- Rate limited: max 5 OTP requests per email per hour
- Resend button available on the verify screen
- Requires `Email.Enabled: true` in production

In development with `Enabled: false`, the OTP is **logged to the console** at `Information` level so you can still test registrations.

---

## Feature: Semantic / Vector Search

When an OpenAI API key is configured, the search bar uses `text-embedding-3-small` to semantically rank issues. The UI shows a **⭐ Semantic Search** badge when vector results are active, and falls back silently to SQL LIKE when not.

### Enable vector search

Edit `appsettings.json`:
```json
"OpenAI": {
  "ApiKey": "sk-...",
  "EmbeddingModel": "text-embedding-3-small"
}
```

How it works:
- Each time an issue is created or edited, its text is embedded in a background task and stored as a JSON float[] in `IssueEmbeddings`
- At search time the query is embedded, all stored vectors loaded into memory, and ranked by cosine similarity
- Results with similarity < 0.30 are filtered out; remaining are passed to EF to filter/paginate
- Falls back to SQL LIKE if OpenAI is unavailable or no embeddings exist yet

---

## Configuration Reference

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=QCPCTracker;..."
  },
  "Email": {
    "Enabled": false,
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "UserName": "",
    "Password": "",
    "FromAddress": "",
    "FromName": "QCPC Issue Tracker"
  },
  "OpenAI": {
    "ApiKey": "",
    "EmbeddingModel": "text-embedding-3-small"
  }
}
```

---

## Architecture

```
Controllers/
  AccountController.cs    — Login, Register (Step 1), VerifyEmail (Step 2), ResendOtp
  IssuesController.cs     — Full CRUD + answers + comments + mentions + export
  HomeController.cs       — Dashboard
  AdminController.cs      — User management

Services/
  IssueService.cs         — Business logic, @mention processing, vector indexing hooks
  EmailService.cs         — MailKit SMTP sender, HTML email templates
  OtpService.cs           — OTP generation (SHA-256 hashed), verify, rate-limit
  VectorSearchService.cs  — OpenAI embeddings via REST, cosine similarity ranking

Models/Models.cs          — ApplicationUser, Issue, IssueAnswer, IssueComment,
                            IssueActivity, Notification, EmailOtp, IssueEmbedding

Data/
  AppDbContext.cs
  DbSeeder.cs             — Seeds roles, 9 users, 12 real issues
```
## Push to docker hub registry

```bash
docker build -t abrahamjo24/qcpc-issue:latest .
docker push abrahamjo24/qcpc-issue:latest
```