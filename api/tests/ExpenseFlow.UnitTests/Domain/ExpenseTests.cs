using ExpenseFlow.Domain.Common;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.UnitTests.Domain;

public class ExpenseTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

    private static Expense NewExpense(decimal amount = 45.50m, string currency = "eur",
        string receipt = "receipts/emp/r1.jpg", DateOnly? date = null, DateTime? now = null) =>
        Expense.Submit(Guid.NewGuid(), EmployeeId, amount, currency, ExpenseCategory.Meals, "Lunch",
            date ?? DateOnly.FromDateTime(Now), receipt, now ?? Now);

    private static Expense InReview()
    {
        var e = NewExpense();
        e.StartReview(Now);
        return e;
    }

    // ---------- Submit ----------

    [Fact]
    public void Submit_ValidInput_IsSubmitted_WithFirstHistoryRow()
    {
        var e = NewExpense();

        Assert.Equal(ExpenseStatus.Submitted, e.Status);
        Assert.Equal("EUR", e.Currency);
        var h = Assert.Single(e.History);
        Assert.Null(h.FromStatus);
        Assert.Equal(ExpenseStatus.Submitted, h.ToStatus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Submit_AmountNotPositive_Throws(decimal amount) =>
        Assert.Throws<DomainException>(() => NewExpense(amount: amount));

    [Fact]
    public void Submit_AmountWithMoreThanTwoDecimals_Throws() =>
        Assert.Throws<DomainException>(() => NewExpense(amount: 10.123m));

    [Fact]
    public void Submit_AmountAboveDecimal12_2_Throws() =>
        Assert.Throws<DomainException>(() => NewExpense(amount: Expense.MaxAmount + 0.01m));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_WithoutReceipt_Throws(string receipt) =>
        Assert.Throws<DomainException>(() => NewExpense(receipt: receipt));

    [Fact]
    public void Submit_FutureDate_Throws() =>
        Assert.Throws<DomainException>(() => NewExpense(date: DateOnly.FromDateTime(Now).AddDays(1)));

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("")]
    public void Submit_InvalidCurrency_Throws(string currency) =>
        Assert.Throws<DomainException>(() => NewExpense(currency: currency));

    [Fact]
    public void Submit_EmptyId_Throws() =>
        Assert.Throws<DomainException>(() => Expense.Submit(Guid.Empty, EmployeeId, 10m, "EUR",
            ExpenseCategory.Meals, null, DateOnly.FromDateTime(Now), "receipts/r.jpg", Now));

    [Fact]
    public void Submit_NonUtcDate_Throws() =>
        Assert.Throws<ArgumentException>(() => NewExpense(now: DateTime.SpecifyKind(Now, DateTimeKind.Local)));

    // ---------- Status flow ----------

    [Fact]
    public void StartReview_FromSubmitted_SetsInReview() =>
        Assert.Equal(ExpenseStatus.InReview, InReview().Status);

    [Fact]
    public void Approve_WhenInReview_SetsApproved_AndDecisionInfo()
    {
        var e = InReview();

        e.Approve(ManagerId, Now);

        Assert.Equal(ExpenseStatus.Approved, e.Status);
        Assert.Equal(ManagerId, e.DecidedById);
        Assert.Equal(Now, e.DecidedAt);
    }

    [Fact]
    public void Approve_WhenStillSubmitted_ThrowsInvalidTransition() =>
        Assert.Throws<InvalidStatusTransitionException>(() => NewExpense().Approve(ManagerId, Now));

    [Fact]
    public void Approve_Twice_ThrowsInvalidTransition()
    {
        var e = InReview();
        e.Approve(ManagerId, Now);

        Assert.Throws<InvalidStatusTransitionException>(() => e.Approve(ManagerId, Now));
    }

    [Fact]
    public void Reject_AfterApprove_ThrowsInvalidTransition()
    {
        var e = InReview();
        e.Approve(ManagerId, Now);

        Assert.Throws<InvalidStatusTransitionException>(() => e.Reject(ManagerId, "Too late", Now));
    }

    // ---------- Rejection reason ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutReason_Throws(string reason) =>
        Assert.Throws<DomainException>(() => InReview().Reject(ManagerId, reason, Now));

    [Fact]
    public void Reject_WithReason_SetsRejected_AndStoresReason()
    {
        var e = InReview();

        e.Reject(ManagerId, "  Missing receipt details ", Now);

        Assert.Equal(ExpenseStatus.Rejected, e.Status);
        Assert.Equal("Missing receipt details", e.RejectionReason);
    }

    // ---------- Self-decision, workflow, history ----------

    [Fact]
    public void Approve_OwnExpense_Throws() =>
        Assert.Throws<DomainException>(() => InReview().Approve(EmployeeId, Now));

    [Fact]
    public void AttachWorkflow_Twice_Throws()
    {
        var e = NewExpense();
        e.AttachWorkflow("wf-1");

        Assert.Throws<InvalidStatusTransitionException>(() => e.AttachWorkflow("wf-2"));
    }

    [Fact]
    public void History_RecordsEveryStatusChange()
    {
        var e = InReview();
        e.Approve(ManagerId, Now);

        Assert.Equal(
            new[] { ExpenseStatus.Submitted, ExpenseStatus.InReview, ExpenseStatus.Approved },
            e.History.Select(h => h.ToStatus));
    }
}
