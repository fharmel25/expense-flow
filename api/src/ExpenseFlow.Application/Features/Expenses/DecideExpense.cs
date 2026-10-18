using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Features.Expenses;

public record ApproveExpenseCommand(Guid ExpenseId) : IRequest<ExpenseSummaryDto>;

public record RejectExpenseCommand(Guid ExpenseId, string Reason) : IRequest<ExpenseSummaryDto>;

public sealed class ApproveExpenseValidator : AbstractValidator<ApproveExpenseCommand>
{
    public ApproveExpenseValidator() => RuleFor(x => x.ExpenseId).NotEmpty();
}

public sealed class RejectExpenseValidator : AbstractValidator<RejectExpenseCommand>
{
    public RejectExpenseValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A rejection reason is required.")
            .MaximumLength(Expense.MaxReasonLength);
    }
}

public sealed class DecideExpenseHandler(
    IAppDbContext db, ICurrentUser currentUser, IClock clock, IWorkflowService workflows)
    : IRequestHandler<ApproveExpenseCommand, ExpenseSummaryDto>,
      IRequestHandler<RejectExpenseCommand, ExpenseSummaryDto>
{
    public Task<ExpenseSummaryDto> Handle(ApproveExpenseCommand cmd, CancellationToken ct) =>
        DecideAsync(cmd.ExpenseId, (expense, managerId) => expense.Approve(managerId, clock.UtcNow), ct);

    public Task<ExpenseSummaryDto> Handle(RejectExpenseCommand cmd, CancellationToken ct) =>
        DecideAsync(cmd.ExpenseId, (expense, managerId) => expense.Reject(managerId, cmd.Reason, clock.UtcNow), ct);

    private async Task<ExpenseSummaryDto> DecideAsync(Guid expenseId, Action<Expense, Guid> decide, CancellationToken ct)
    {
        var managerId = currentUser.UserId;
        var expense = await db.Expenses.Include(e => e.Employee).FirstOrDefaultAsync(e => e.Id == expenseId, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);

        var manager = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == managerId, ct);
        if (manager is null || !manager.IsManagerOf(expense.Employee))
            throw new ForbiddenException("Only this employee's manager can decide on this expense.");

        decide(expense, managerId); // Domain: must be InReview, reason required, no self-decision
        await db.SaveChangesAsync(ct);

        // The workflow continues: it notifies the employee (Phase 5).
        if (expense.WorkflowInstanceId is not null)
            await workflows.ResumeAfterDecisionAsync(expense.WorkflowInstanceId, expense.Id, ct);

        return await db.Expenses.AsNoTracking().Where(e => e.Id == expense.Id)
            .Select(ExpenseMapping.ToSummary).FirstAsync(ct);
    }
}
