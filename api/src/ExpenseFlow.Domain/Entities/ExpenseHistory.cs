using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities;

public class ExpenseHistory
{
    public Guid Id { get; private set; }
    public Guid ExpenseId { get; private set; }
    public ExpenseStatus? FromStatus { get; private set; }
    public ExpenseStatus ToStatus { get; private set; }
    public Guid? ChangedById { get; private set; } // null = changed by the system (workflow)
    public DateTime ChangedAt { get; private set; }
    public string? Comment { get; private set; }

    private ExpenseHistory() { } // required by EF Core

    internal ExpenseHistory(Guid expenseId, ExpenseStatus? fromStatus, ExpenseStatus toStatus,
        Guid? changedById, DateTime changedAt, string? comment)
    {
        Id = Guid.NewGuid();
        ExpenseId = expenseId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedById = changedById;
        ChangedAt = changedAt;
        Comment = comment;
    }
}
