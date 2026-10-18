using System.Linq.Expressions;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Features.Expenses;

public record ExpenseSummaryDto(
    Guid Id, decimal Amount, string Currency, ExpenseCategory Category, string? Description,
    DateOnly ExpenseDate, ExpenseStatus Status, DateTime SubmittedAt, Guid EmployeeId, string EmployeeName);

public record ExpenseHistoryDto(
    ExpenseStatus? FromStatus, ExpenseStatus ToStatus, Guid? ChangedById, DateTime ChangedAt, string? Comment);

public record ExpenseDetailDto(
    Guid Id, decimal Amount, string Currency, ExpenseCategory Category, string? Description,
    DateOnly ExpenseDate, ExpenseStatus Status, DateTime SubmittedAt, Guid EmployeeId, string EmployeeName,
    Uri ReceiptUrl, DateTime? DecidedAt, Guid? DecidedById, string? RejectionReason,
    IReadOnlyList<ExpenseHistoryDto> History);

internal static class ExpenseMapping
{
    /// <summary>Projection translated to SQL by EF Core: only the needed columns are read.</summary>
    public static readonly Expression<Func<Expense, ExpenseSummaryDto>> ToSummary = e => new ExpenseSummaryDto(
        e.Id, e.Amount, e.Currency, e.Category, e.Description,
        e.ExpenseDate, e.Status, e.SubmittedAt, e.EmployeeId, e.Employee.FullName);
}
