using Microsoft.AspNetCore.Identity;
using QCPCMvc.Models;

namespace QCPCMvc.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider svc)
    {
        using var scope  = svc.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.EnsureCreatedAsync();

        // Roles
        foreach (var r in new[]{"Admin","QualityManager","TeamLead","Developer","Viewer"})
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        // Seed Departments
        var departments = new[]
        {
            new Department { Name = "SAP ABAP & Integration", GroupEmail = "sap-integration@ethiopianairlines.com" },
        };

        foreach (var dept in departments)
        {
            if (!db.Departments.Any(d => d.Name == dept.Name))
                db.Departments.Add(dept);
        }
        await db.SaveChangesAsync();

        // Get department IDs
        var sapDept = db.Departments.FirstOrDefault(d => d.Name == "SAP ABAP & Integration");

        // Admin
        if (await userMgr.FindByEmailAsync("admin@qcpc.local") == null)
        {
            var a = new ApplicationUser { UserName="admin@qcpc.local", Email="admin@qcpc.local",
                FullName="QCPC Administrator", DepartmentId = sapDept?.Id, EmailConfirmed=true };
            var r = await userMgr.CreateAsync(a, "Admin@1234!");
            if (r.Succeeded) await userMgr.AddToRoleAsync(a, "Admin");
        }

        // Sample team members
        var people = new[]
        {
            ("solomonmk@ethiopianairlines.com",  "Solomon Melaku",   "SAP ABAP & Integration",    "Developer", sapDept?.Id),
            ("talegetam@ethiopianairlines.com","Taleget Mandefro", "SAP ABAP & Integration",    "Developer", sapDept?.Id),
            ("MikiasWon@ethiopianairlines.com",   "Mikias Wendim",    "SAP ABAP & Integration",    "Developer", sapDept?.Id),
            ("sewlesewb@ethiopianairlines.com", "Sewlesew Biazen",  "SAP ABAP & Integration",            "Developer", sapDept?.Id),
            ("habtamuaw@ethiopianairlines.com",   "Habtamu Awoke",    "SAP ABAP & Integration",       "TeamLead", sapDept?.Id),
            ("lemim@ethiopianairlines.com",    "Lemi Melkamu",     "SAP ABAP & Integration", "Developer", sapDept?.Id),
            ("jibrilad@ethiopianairlines.com",     "Jibril Adem",      "SAP ABAP & Integration",     "Developer", sapDept?.Id),
            ("geremeww@ethiopianairlines.com",   "Geremew Werku",    "SAP ABAP & Integration",     "Developer", sapDept?.Id),
        };
        foreach (var (email, name, dept, role, deptId) in people)
        {
            if (await userMgr.FindByEmailAsync(email) == null)
            {
                var u = new ApplicationUser { UserName=email, Email=email,
                    FullName=name, DepartmentId = deptId, EmailConfirmed=true };
                var r = await userMgr.CreateAsync(u, "User@1234!");
                if (r.Succeeded) await userMgr.AddToRoleAsync(u, role);
            }
        }

        if (!db.Issues.Any())
        {
            var solomon  = (await userMgr.FindByEmailAsync("solomonmk@ethiopianairlines.com"))!;
            var taleget  = (await userMgr.FindByEmailAsync("talegetam@ethiopianairlines.com"))!;
            var mikias   = (await userMgr.FindByEmailAsync("MikiasWon@ethiopianairlines.com"))!;
            var sewlesew = (await userMgr.FindByEmailAsync("sewlesewb@ethiopianairlines.com"))!;
            var habtamu  = (await userMgr.FindByEmailAsync("habtamuaw@ethiopianairlines.com"))!;
            var lemi     = (await userMgr.FindByEmailAsync("lemim@ethiopianairlines.com"))!;
            var jibril   = (await userMgr.FindByEmailAsync("jibrilad@ethiopianairlines.com"))!;

            var issues = new List<Issue>
            {
                new(){ ResponseNumber=1, Title="DB connection issue with MS SQL in Anypoint Studio",
                  Description="DB connection issue when trying to use Database connector for MS SQL Database in Anypoint Studio. Compatibility issue with the provided MS SQL Server Driver.",
                  Type=IssueType.Internal, Priority=IssuePriority.Critical, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Review the provided MS SQL server Driver, check if any configuration parameter is missing",
                  ResponsibleTeam="TPM", Admitted=true, Resolved=true, Tags="mulesoft,database,sql",
                  ReceivedDate=new DateTime(2025,6,17), PromisedDate=new DateTime(2025,7,20),
                  CompletedDate=new DateTime(2025,7,20), SubmittedById=solomon.Id },

                new(){ ResponseNumber=2, Title="Unable to update Documents in ACE Portal",
                  Description="Unable to update Documents in ACE Portal since some days. Issue with the Outlook exchange server.",
                  Type=IssueType.Internal, Priority=IssuePriority.Medium, Process=IssueProcess.OperatingMonitoring,
                  Status=IssueStatus.Resolved, CorrectiveAction="Escalate to help Desk if issue persists",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=true, Tags="portal,outlook,exchange",
                  ReceivedDate=new DateTime(2025,7,25), PromisedDate=new DateTime(2025,7,29),
                  CompletedDate=new DateTime(2025,7,29), SubmittedById=solomon.Id },

                new(){ ResponseNumber=3, Title="WMS API unreachable from internal network",
                  Description="Unable to access the published WMS API address from the internal network. Error: 'This site can't be reached — wms.ethiopianairlines.com refused to connect.'",
                  Type=IssueType.Internal, Priority=IssuePriority.Medium, Process=IssueProcess.Deploying,
                  Status=IssueStatus.Resolved, CorrectiveAction="Informed the IT Service Desk to investigate and resolve",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=true, Tags="api,network,wms",
                  ReceivedDate=new DateTime(2025,7,29), PromisedDate=new DateTime(2025,7,29),
                  CompletedDate=new DateTime(2025,7,29), SubmittedById=taleget.Id },

                new(){ ResponseNumber=4, Title="Unable to connect to Git remote from Anypoint Studio",
                  Description="Unable to connect to git remote from Anypoint Studio. The ET domain credential was used instead of the Azure DevOps git credential.",
                  Type=IssueType.Internal, Priority=IssuePriority.Low, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Get the git credential from Azure DevOps and use that as the username and password.",
                  ResponsibleTeam="TPM", Admitted=true, Resolved=true, Tags="git,azure-devops,credentials",
                  ReceivedDate=new DateTime(2025,7,31), PromisedDate=new DateTime(2025,7,31),
                  CompletedDate=new DateTime(2025,7,31), SubmittedById=mikias.Id },

                new(){ ResponseNumber=5, Title="Invalid attributes in HTTP request config XML",
                  Description="Attribute 'body' and 'uri-params' are not allowed in element 'http:request-config'. These attributes were incorrectly included in the HTTP request configuration XML.",
                  Type=IssueType.Internal, Priority=IssuePriority.High, Process=IssueProcess.Designing,
                  Status=IssueStatus.Resolved, CorrectiveAction="Remove those attributes from the xml configuration",
                  ResponsibleTeam="QCPC", Admitted=true, Resolved=true, Tags="mulesoft,xml,configuration",
                  ReceivedDate=new DateTime(2025,7,31), PromisedDate=new DateTime(2025,7,31),
                  CompletedDate=new DateTime(2025,7,31), SubmittedById=taleget.Id },

                new(){ ResponseNumber=6, Title="SAP Quality server (ERQ) not responding",
                  Description="The SAP Quality server (ERQ) is not responding when trying to logon. Server appears to be down.",
                  Type=IssueType.Internal, Priority=IssuePriority.Critical, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Sent a request to SERVICE DESK to restart the server",
                  ResponsibleTeam="TPM", Admitted=true, Resolved=true, Tags="sap,server,erq",
                  ReceivedDate=new DateTime(2025,8,25), PromisedDate=new DateTime(2025,8,25),
                  CompletedDate=new DateTime(2025,8,25), SubmittedById=sewlesew.Id },

                new(){ ResponseNumber=7, Title="MS SQL DB connector driver dependency failure in MuleSoft",
                  Description="Not able to specify driver dependency for MS SQL DB connector in MuleSoft Studio. The 'Add Maven dependency' option failed to download the required driver.",
                  Type=IssueType.Internal, Priority=IssuePriority.Critical, Process=IssueProcess.TestingAndQA,
                  Status=IssueStatus.Resolved, CorrectiveAction="Look for alternate options online for driver dependency",
                  ResponsibleTeam="TPM", Admitted=true, Resolved=true, Tags="mulesoft,maven,database",
                  ReceivedDate=new DateTime(2025,8,31), PromisedDate=new DateTime(2025,9,5),
                  CompletedDate=new DateTime(2025,9,5), SubmittedById=solomon.Id },

                new(){ ResponseNumber=8, Title="No predefined project template for Next.js",
                  Description="Starting a new Next.js project takes significant time. No predefined project template available that integrates with Ethiopian Airlines IAM.",
                  Type=IssueType.Internal, Priority=IssuePriority.High, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Prepare and maintain a reusable startup template for Next.js projects integrating with ET IAM.",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=true, Tags="nextjs,template,frontend",
                  ReceivedDate=new DateTime(2025,8,31), PromisedDate=new DateTime(2025,9,5),
                  CompletedDate=new DateTime(2025,9,5), SubmittedById=habtamu.Id },

                new(){ ResponseNumber=9, Title="IIS staging environment setup for STMS system was challenging",
                  Description="Setting up staging environment for STMS system on IIS was challenging. No server access, and configuring web.config for vite/react app with Node authentication was complex.",
                  Type=IssueType.Internal, Priority=IssuePriority.Medium, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Development server access should be granted for developers upon request. Used Linux server as a last resort.",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=true, Tags="iis,staging,deployment",
                  ReceivedDate=new DateTime(2025,8,31), PromisedDate=new DateTime(2025,8,31),
                  CompletedDate=new DateTime(2025,8,31), SubmittedById=lemi.Id },

                new(){ ResponseNumber=10, Title="Salesforce DUPLICATES_DETECTED error during IATA data interface",
                  Description="While trying to interface IATA Agent file data to Salesforce, system throws DUPLICATES_DETECTED error. Blocking rule with Account number/External Id was the cause.",
                  Type=IssueType.Internal, Priority=IssuePriority.High, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Modified the blocking rule to update existing records instead of blocking.",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=true, Tags="salesforce,iata,duplicates",
                  ReceivedDate=new DateTime(2025,9,22), PromisedDate=new DateTime(2025,9,23),
                  CompletedDate=new DateTime(2025,9,23), SubmittedById=jibril.Id },

                new(){ ResponseNumber=11, Title="Repeated internet disconnections in work area",
                  Description="Internet interruption — disconnecting repeatedly. Likely caused by limited number of WiFi routers in the work area.",
                  Type=IssueType.Internal, Priority=IssuePriority.Critical, Process=IssueProcess.OperatingMonitoring,
                  Status=IssueStatus.InProgress, CorrectiveAction="Add Router. Issue escalated to IT Infrastructure Team.",
                  ResponsibleTeam="RCCA", Admitted=true, Resolved=false, Tags="network,wifi,infrastructure",
                  ReceivedDate=new DateTime(2025,9,25), PromisedDate=new DateTime(2025,10,10),
                  SubmittedById=solomon.Id },

                new(){ ResponseNumber=12, Title="Dev-IAM login not functioning correctly",
                  Description="The dev-IAM login either allows login but only displays Dashboard without content, or returns: 'An unexpected error occurred — Could not get the user roles'.",
                  Type=IssueType.Internal, Priority=IssuePriority.Medium, Process=IssueProcess.Implementation,
                  Status=IssueStatus.Resolved, CorrectiveAction="Communicated with IAM support/development team to debug the issues",
                  ResponsibleTeam="QCPC", Admitted=true, Resolved=true, Tags="iam,authentication,roles",
                  ReceivedDate=new DateTime(2025,9,28), PromisedDate=new DateTime(2025,10,10),
                  CompletedDate=new DateTime(2025,10,10), SubmittedById=sewlesew.Id },
            };

            db.Issues.AddRange(issues);
            await db.SaveChangesAsync();
        }
    }
}
