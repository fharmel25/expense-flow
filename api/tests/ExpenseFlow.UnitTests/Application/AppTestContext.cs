using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseHistory> ExpenseHistory => Set<ExpenseHistory>();
    public DbSet<PushToken> PushTokens => Set<PushToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Ids are created by the Domain, not by the database.
        b.Entity<User>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<Expense>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ExpenseHistory>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<PushToken>().Property(x => x.Id).ValueGeneratedNever();
    }
}

/// <summary>Two teams: Manager → Employee, OtherManager → OtherEmployee. Fakes for every outside service.</summary>
public sealed class AppTestContext : IDisposable
{
    public static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    public TestDbContext Db { get; }
    public IClock Clock { get; } = Substitute.For<IClock>();
    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();
    public IReceiptStorage Receipts { get; } = Substitute.For<IReceiptStorage>();
    public IWorkflowService Workflows { get; } = Substitute.For<IWorkflowService>();
    public INotificationClient Notifications { get; } = Substitute.For<INotificationClient>();
    public IPasswordHasher Hasher { get; } = Substitute.For<IPasswordHasher>();
    public ITokenService Tokens { get; } = Substitute.For<ITokenService>();

    public User Manager { get; }
    public User Employee { get; }
    public User OtherManager { get; }
    public User OtherEmployee { get; }

    public AppTestContext()
    {
        Db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        Clock.UtcNow.Returns(Now);
        Receipts.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Receipts.CreateReadUrl(Arg.Any<string>()).Returns(new Uri("https://storage.test/receipt?sas"));
        Workflows.StartApprovalAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("wf-1");

        Manager = new User("manager@test.com", "hash", "Mia Manager", UserRole.Manager, null, Now);
        OtherManager = new User("other.manager@test.com", "hash", "Omar Other", UserRole.Manager, null, Now);
        Employee = new User("employee@test.com", "hash", "Eva Employee", UserRole.Employee, Manager.Id, Now);
        OtherEmployee = new User("other.employee@test.com", "hash", "Oscar Employee", UserRole.Employee, OtherManager.Id, Now);
        Db.Users.AddRange(Manager, OtherManager, Employee, OtherEmployee);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();

        LoginAs(Employee);
    }

    public void LoginAs(User user) => CurrentUser.UserId.Returns(user.Id);

    /// <summary>Adds an expense with a workflow attached, by default already InReview.</summary>
    public Expense AddExpense(User employee, bool inReview = true)
    {
        var e = Expense.Submit(Guid.NewGuid(), employee.Id, 45.50m, "EUR", ExpenseCategory.Meals, "Lunch",
            DateOnly.FromDateTime(Now), $"receipts/{employee.Id}/r.jpg", Now);
        e.AttachWorkflow("wf-" + e.Id);
        if (inReview) e.StartReview(Now);
        Db.Expenses.Add(e);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return e;
    }

    public void AddPushToken(User user, string token)
    {
        Db.PushTokens.Add(new PushToken(user.Id, token, DevicePlatform.Android, Now));
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public void Dispose() => Db.Dispose();
}
