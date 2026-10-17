using ExpenseFlow.Domain.Common;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public Guid? ManagerId { get; private set; }
    public User? Manager { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { } // required by EF Core

    public User(string email, string passwordHash, string fullName, UserRole role, Guid? managerId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Email is required.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Full name is required.");
        UtcGuard.Ensure(utcNow);

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
        ManagerId = managerId;
        CreatedAt = utcNow;
    }

    /// <summary>Rule: a manager can only decide on expenses of their own team.</summary>
    public bool IsManagerOf(User employee) =>
        Role == UserRole.Manager && employee.ManagerId == Id;
}
