using ExpenseFlow.Application.Common;
using ExpenseFlow.Application.Features.Expenses;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

public class DecideExpenseTests : IDisposable
{
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    private DecideExpenseHandler Handler() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Clock, _ctx.Workflows);

    [Fact]
    public async Task Approve_ByTeamManager_Approves_AndResumesWorkflow()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.Manager);

        var result = await Handler().Handle(new ApproveExpenseCommand(e.Id), default);

        Assert.Equal(ExpenseStatus.Approved, result.Status);
        await _ctx.Workflows.Received(1).ResumeAfterDecisionAsync(e.WorkflowInstanceId!, e.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_ByTeamManager_StoresReason_AndResumesWorkflow()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.Manager);

        await Handler().Handle(new RejectExpenseCommand(e.Id, "Missing details"), default);

        var saved = await _ctx.Db.Expenses.SingleAsync(x => x.Id == e.Id);
        Assert.Equal(ExpenseStatus.Rejected, saved.Status);
        Assert.Equal("Missing details", saved.RejectionReason);
        await _ctx.Workflows.Received(1).ResumeAfterDecisionAsync(e.WorkflowInstanceId!, e.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_ByOtherTeamManager_IsForbidden_AndNothingChanges()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.OtherManager);

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler().Handle(new ApproveExpenseCommand(e.Id), default));

        Assert.Equal(ExpenseStatus.InReview, (await _ctx.Db.Expenses.SingleAsync(x => x.Id == e.Id)).Status);
        await _ctx.Workflows.DidNotReceiveWithAnyArgs().ResumeAfterDecisionAsync(default!, default, default);
    }

    [Fact]
    public async Task Approve_ByEmployee_IsForbidden()
    {
        var e = _ctx.AddExpense(_ctx.Employee); // logged in as Employee by default

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler().Handle(new ApproveExpenseCommand(e.Id), default));
    }

    [Fact]
    public async Task Approve_UnknownExpense_IsNotFound()
    {
        _ctx.LoginAs(_ctx.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(new ApproveExpenseCommand(Guid.NewGuid()), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectValidator_WithoutReason_Fails(string reason) =>
        Assert.False(new RejectExpenseValidator().Validate(new RejectExpenseCommand(Guid.NewGuid(), reason)).IsValid);
}
