using ExpenseFlow.Application.Common;
using ExpenseFlow.Application.Features.Expenses;
using ExpenseFlow.Domain.Common;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

public class SubmitExpenseTests : IDisposable
{
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    private SubmitExpenseHandler Handler() =>
        new(_ctx.Db, _ctx.CurrentUser, _ctx.Clock, _ctx.Receipts, _ctx.Workflows);

    private SubmitExpenseCommand Command(Guid? id = null, string? receipt = null) => new(
        id ?? Guid.NewGuid(), 45.50m, "EUR", ExpenseCategory.Meals, "Lunch",
        DateOnly.FromDateTime(AppTestContext.Now), receipt ?? $"receipts/{_ctx.Employee.Id}/r1.jpg");

    [Fact]
    public async Task Submit_Valid_SavesExpense_StartsOneWorkflow_AndAttachesIt()
    {
        var cmd = Command();

        var result = await Handler().Handle(cmd, default);

        Assert.Equal(ExpenseStatus.Submitted, result.Status);
        Assert.Equal("wf-1", (await _ctx.Db.Expenses.SingleAsync()).WorkflowInstanceId);
        await _ctx.Workflows.Received(1).StartApprovalAsync(cmd.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_SameIdTwice_ReturnsExisting_WithoutDuplicate()
    {
        var cmd = Command();
        await Handler().Handle(cmd, default);
        _ctx.Db.ChangeTracker.Clear();

        await Handler().Handle(cmd, default);

        Assert.Equal(1, await _ctx.Db.Expenses.CountAsync());
        await _ctx.Workflows.Received(1).StartApprovalAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_RetryAfterWorkflowFailure_StartsTheWorkflow()
    {
        var cmd = Command();
        _ctx.Workflows.StartApprovalAsync(cmd.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("Elsa down")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, default));
        _ctx.Db.ChangeTracker.Clear();
        _ctx.Workflows.StartApprovalAsync(cmd.Id, Arg.Any<CancellationToken>()).Returns("wf-2");

        await Handler().Handle(cmd, default);

        Assert.Equal("wf-2", (await _ctx.Db.Expenses.SingleAsync()).WorkflowInstanceId);
    }

    [Fact]
    public async Task Submit_ReceiptOfAnotherUser_IsForbidden_AndNothingSaved()
    {
        var cmd = Command(receipt: $"receipts/{_ctx.OtherEmployee.Id}/r1.jpg");

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler().Handle(cmd, default));
        Assert.Equal(0, await _ctx.Db.Expenses.CountAsync());
    }

    [Fact]
    public async Task Submit_ReceiptNotUploaded_Throws_AndNothingSaved()
    {
        _ctx.Receipts.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(Command(), default));
        Assert.Equal(0, await _ctx.Db.Expenses.CountAsync());
        await _ctx.Workflows.DidNotReceiveWithAnyArgs().StartApprovalAsync(default, default);
    }

    [Fact]
    public async Task Submit_IdAlreadyUsedByAnotherEmployee_IsConflict()
    {
        var cmd = Command();
        await Handler().Handle(cmd, default);
        _ctx.Db.ChangeTracker.Clear();
        _ctx.LoginAs(_ctx.OtherEmployee);

        await Assert.ThrowsAsync<ConflictException>(() => Handler().Handle(
            cmd with { ReceiptBlobName = $"receipts/{_ctx.OtherEmployee.Id}/x.jpg" }, default));
    }

    [Fact]
    public void Validator_ValidInput_Passes() =>
        Assert.True(new SubmitExpenseValidator().Validate(Command()).IsValid);

    [Theory]
    [InlineData(0, "EUR")]
    [InlineData(-5, "EUR")]
    [InlineData(10.123, "EUR")]
    [InlineData(10, "EU")]
    public void Validator_InvalidInput_Fails(double amount, string currency) =>
        Assert.False(new SubmitExpenseValidator()
            .Validate(Command() with { Amount = (decimal)amount, Currency = currency }).IsValid);
}
