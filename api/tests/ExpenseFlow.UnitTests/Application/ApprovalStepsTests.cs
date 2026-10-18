using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Features.Expenses;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

/// <summary>CLAUDE.md rule: every decision triggers a push notification. Failures are never reported as sent.</summary>
public class ApprovalStepsTests : IDisposable
{
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    public ApprovalStepsTests() =>
        _ctx.Notifications.SendAsync(default!, default!, default)
            .ReturnsForAnyArgs(new NotificationResult(1, Array.Empty<string>()));

    private ApprovalSteps Steps() => new(_ctx.Db, _ctx.Clock, _ctx.Notifications, NullLogger<ApprovalSteps>.Instance);

    private Expense Decided(bool approve)
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        var tracked = _ctx.Db.Expenses.Single(x => x.Id == e.Id);
        if (approve) tracked.Approve(_ctx.Manager.Id, AppTestContext.Now);
        else tracked.Reject(_ctx.Manager.Id, "Missing details", AppTestContext.Now);
        _ctx.Db.SaveChanges();
        _ctx.Db.ChangeTracker.Clear();
        return tracked;
    }

    [Fact]
    public async Task NotifyManager_SendsOnlyToTheManagersDevices()
    {
        _ctx.AddPushToken(_ctx.Manager, "ExpoPushToken[manager]");
        _ctx.AddPushToken(_ctx.Employee, "ExpoPushToken[employee]");
        var e = _ctx.AddExpense(_ctx.Employee);

        Assert.True(await Steps().NotifyManagerAsync(e.Id, default));

        await _ctx.Notifications.Received(1).SendAsync(
            Arg.Is<IReadOnlyCollection<string>>(t => t.Single() == "ExpoPushToken[manager]"),
            Arg.Is<NotificationMessage>(m => m.Title == "New expense to review"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyEmployee_AfterApproval_SendsApprovedMessage()
    {
        _ctx.AddPushToken(_ctx.Employee, "ExpoPushToken[employee]");
        var e = Decided(approve: true);

        Assert.True(await Steps().NotifyEmployeeAsync(e.Id, default));

        await _ctx.Notifications.Received(1).SendAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Is<NotificationMessage>(m => m.Title == "Expense approved" && m.Data!["expenseId"] == e.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyEmployee_AfterRejection_IncludesTheReason()
    {
        _ctx.AddPushToken(_ctx.Employee, "ExpoPushToken[employee]");
        var e = Decided(approve: false);

        Assert.True(await Steps().NotifyEmployeeAsync(e.Id, default));

        await _ctx.Notifications.Received(1).SendAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Is<NotificationMessage>(m => m.Title == "Expense rejected" && m.Body.Contains("Missing details")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_DeadToken_IsDeleted()
    {
        _ctx.AddPushToken(_ctx.Employee, "ExpoPushToken[dead]");
        _ctx.Notifications.SendAsync(default!, default!, default)
            .ReturnsForAnyArgs(new NotificationResult(0, new[] { "ExpoPushToken[dead]" }));
        var e = Decided(approve: true);

        await Steps().NotifyEmployeeAsync(e.Id, default);

        Assert.Equal(0, await _ctx.Db.PushTokens.CountAsync());
    }

    [Fact]
    public async Task Notify_WhenServiceFails_ReturnsFalse_NeverReportedAsSent()
    {
        _ctx.AddPushToken(_ctx.Employee, "ExpoPushToken[employee]");
        _ctx.Notifications.SendAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException<NotificationResult>(new HttpRequestException("Node down")));
        var e = Decided(approve: true);

        Assert.False(await Steps().NotifyEmployeeAsync(e.Id, default));
    }

    [Fact]
    public async Task Notify_UserWithoutDevice_ReturnsFalse_AndDoesNotCallService()
    {
        var e = Decided(approve: true);

        Assert.False(await Steps().NotifyEmployeeAsync(e.Id, default));
        await _ctx.Notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task StartReview_CalledTwice_IsIdempotent()
    {
        var e = _ctx.AddExpense(_ctx.Employee, inReview: false);

        await Steps().StartReviewAsync(e.Id, default);
        _ctx.Db.ChangeTracker.Clear();
        await Steps().StartReviewAsync(e.Id, default);

        var saved = await _ctx.Db.Expenses.Include(x => x.History).SingleAsync(x => x.Id == e.Id);
        Assert.Equal(ExpenseStatus.InReview, saved.Status);
        Assert.Equal(2, saved.History.Count); // Submitted + InReview, not a duplicate
    }
}
