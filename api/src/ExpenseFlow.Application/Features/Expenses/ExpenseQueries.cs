using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Features.Expenses;

// ---------- My expenses ----------

public record GetMyExpensesQuery : IRequest<IReadOnlyList<ExpenseSummaryDto>>;

public sealed class GetMyExpensesHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyExpensesQuery, IReadOnlyList<ExpenseSummaryDto>>
{
    public async Task<IReadOnlyList<ExpenseSummaryDto>> Handle(GetMyExpensesQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        return await db.Expenses.AsNoTracking()
            .Where(e => e.EmployeeId == userId)
            .OrderByDescending(e => e.SubmittedAt)
            .Take(200)
            .Select(ExpenseMapping.ToSummary)
            .ToListAsync(ct);
    }
}

// ---------- Expense detail (owner or that employee's manager only) ----------

public record GetExpenseByIdQuery(Guid Id) : IRequest<ExpenseDetailDto>;

public sealed class GetExpenseByIdHandler(IAppDbContext db, ICurrentUser currentUser, IReceiptStorage receipts)
    : IRequestHandler<GetExpenseByIdQuery, ExpenseDetailDto>
{
    public async Task<ExpenseDetailDto> Handle(GetExpenseByIdQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var e = await db.Expenses.AsNoTracking()
            .Include(x => x.Employee)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct)
            ?? throw new NotFoundException(nameof(Expense), query.Id);

        if (e.EmployeeId != userId)
        {
            var caller = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (caller is null || !caller.IsManagerOf(e.Employee))
                throw new ForbiddenException("You cannot view this expense.");
        }

        return new ExpenseDetailDto(
            e.Id, e.Amount, e.Currency, e.Category, e.Description, e.ExpenseDate, e.Status, e.SubmittedAt,
            e.EmployeeId, e.Employee.FullName, receipts.CreateReadUrl(e.ReceiptBlobName),
            e.DecidedAt, e.DecidedById, e.RejectionReason,
            e.History.OrderBy(h => h.ChangedAt)
                .Select(h => new ExpenseHistoryDto(h.FromStatus, h.ToStatus, h.ChangedById, h.ChangedAt, h.Comment))
                .ToList());
    }
}

// ---------- Pending approvals (managers, own team only) ----------

public record GetPendingExpensesQuery : IRequest<IReadOnlyList<ExpenseSummaryDto>>;

public sealed class GetPendingExpensesHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPendingExpensesQuery, IReadOnlyList<ExpenseSummaryDto>>
{
    public async Task<IReadOnlyList<ExpenseSummaryDto>> Handle(GetPendingExpensesQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var caller = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (caller is null || caller.Role != UserRole.Manager)
            throw new ForbiddenException("Only managers can see pending expenses.");

        return await db.Expenses.AsNoTracking()
            .Where(e => e.Status == ExpenseStatus.InReview && e.Employee.ManagerId == userId)
            .OrderBy(e => e.SubmittedAt)
            .Select(ExpenseMapping.ToSummary)
            .ToListAsync(ct);
    }
}
