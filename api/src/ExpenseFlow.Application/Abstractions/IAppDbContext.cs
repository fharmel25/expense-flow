using ExpenseFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseHistory> ExpenseHistory { get; }
    DbSet<PushToken> PushTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
