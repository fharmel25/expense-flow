using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    private static User Manager() => new("boss@test.com", "hash", "Boss", UserRole.Manager, null, Now);

    [Fact]
    public void IsManagerOf_OwnTeamMember_ReturnsTrue()
    {
        var manager = Manager();
        var employee = new User("emp@test.com", "hash", "Emp", UserRole.Employee, manager.Id, Now);

        Assert.True(manager.IsManagerOf(employee));
    }

    [Fact]
    public void IsManagerOf_OtherTeamMember_ReturnsFalse()
    {
        var manager = Manager();
        var otherManager = Manager();
        var employee = new User("emp@test.com", "hash", "Emp", UserRole.Employee, otherManager.Id, Now);

        Assert.False(manager.IsManagerOf(employee));
    }

    [Fact]
    public void IsManagerOf_WhenRoleIsEmployee_ReturnsFalse()
    {
        var notAManager = new User("x@test.com", "hash", "X", UserRole.Employee, null, Now);
        var employee = new User("emp@test.com", "hash", "Emp", UserRole.Employee, notAManager.Id, Now);

        Assert.False(notAManager.IsManagerOf(employee));
    }

    [Fact]
    public void Constructor_NormalizesEmail() =>
        Assert.Equal("john@test.com",
            new User("  John@Test.com ", "hash", "John", UserRole.Employee, null, Now).Email);
}
