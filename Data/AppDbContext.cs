using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QCPCMvc.Models;

namespace QCPCMvc.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }

    public DbSet<Issue>          Issues         { get; set; }
    public DbSet<IssueAnswer>    IssueAnswers   { get; set; }
    public DbSet<IssueComment>   IssueComments  { get; set; }
    public DbSet<IssueActivity>  IssueActivities{ get; set; }
    public DbSet<Notification>   Notifications  { get; set; }
    public DbSet<EmailOtp>       EmailOtps      { get; set; }
    public DbSet<IssueEmbedding> IssueEmbeddings{ get; set; }
    public DbSet<Department>     Departments    { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Issue>(e => {
            e.HasOne(i => i.SubmittedBy).WithMany(u => u.SubmittedIssues)
             .HasForeignKey(i => i.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.AssignedTo).WithMany()
             .HasForeignKey(i => i.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<IssueAnswer>(e => {
            e.HasOne(a => a.Issue).WithMany(i => i.Answers)
             .HasForeignKey(a => a.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Author).WithMany(u => u.Answers)
             .HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<IssueComment>(e => {
            e.HasOne(c => c.Issue).WithMany(i => i.Comments)
             .HasForeignKey(c => c.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Answer).WithMany(a => a.Comments)
             .HasForeignKey(c => c.AnswerId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(c => c.Parent).WithMany(p => p.Replies)
             .HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Author).WithMany(u => u.Comments)
             .HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<IssueActivity>(e => {
            e.HasOne(a => a.Issue).WithMany(i => i.Activities)
             .HasForeignKey(a => a.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Actor).WithMany()
             .HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Notification>(e => {
            e.HasOne(n => n.User).WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<IssueEmbedding>(e => {
            e.HasKey(x => x.IssueId);
            e.HasOne(x => x.Issue).WithOne()
             .HasForeignKey<IssueEmbedding>(x => x.IssueId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.VectorJson).HasColumnType("nvarchar(max)");
        });

        b.Entity<EmailOtp>(e => {
            e.HasIndex(x => x.Email);
        });

        b.Entity<Department>(e => {
            e.HasIndex(x => x.Name).IsUnique();
        });
    }
}
