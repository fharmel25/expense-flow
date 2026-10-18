using System.Globalization;
using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseFlow.Application.Features.Expenses;

/// <summary>
/// Steps of the approval workflow, called by the Elsa activities (Phase 5).
/// The business logic lives here; the workflow only decides the order.
/// </summary>
public sealed class ApprovalSteps(
    IAppDbContext db, IClock clock, INotificationClient notifications, ILogger<ApprovalSteps> logger)
{
    private const int MaxReasonInNotification = 120;

    /// <summary>Submitted → InReview. Safe to call twice (workflow retries).</summary>
    public async Task StartReviewAsync(Guid expenseId, CancellationToken ct)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId, ct)
            ?? throw new NotFoundException(nameof(Expense), expenseId);
        if (expense.Status != ExpenseStatus.Submitted) return;

        expense.StartReview(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <returns>true only if at least one notification was really accepted.</returns>
    public async Task<bool> NotifyManagerAsync(Guid expenseId, CancellationToken ct)
    {
        var expense = await LoadWithEmployeeAsync(expenseId, ct);
        if (expense.Employee.ManagerId is not Guid managerId)
        {
            logger.LogWarning("Expense {ExpenseId}: employee has no manager, nobody to notify.", expenseId);
            return false;
        }

        var message = new NotificationMessage(
            "New expense to review", $"{expense.Employee.FullName} submitted {Money(expense)}.", Data(expense));
        return await SendToUserAsync(managerId, message, ct);
    }

    /// <returns>true only if at least one notification was really accepted.</returns>
    public async Task<bool> NotifyEmployeeAsync(Guid expenseId, CancellationToken ct)
    {
        var expense = await LoadWithEmployeeAsync(expenseId, ct);
        NotificationMessage? message = expense.Status switch
        {
            ExpenseStatus.Approved => new("Expense approved",
                $"Your expense of {Money(expense)} was approved.", Data(expense)),
            ExpenseStatus.Rejected => new("Expense rejected",
                $"Your expense of {Money(expense)} was rejected: {Truncate(expense.RejectionReason)}", Data(expense)),
            _ => null
        };

        if (message is null)
        {
            logger.LogWarning("Expense {ExpenseId} has no decision yet (status {Status}); employee not notified.",
                expenseId, expense.Status);
            return false;
        }
        return await SendToUserAsync(expense.EmployeeId, message, ct);
    }

    private async Task<Expense> LoadWithEmployeeAsync(Guid expenseId, CancellationToken ct) =>
        await db.Expenses.AsNoTracking().Include(e => e.Employee).FirstOrDefaultAsync(e => e.Id == expenseId, ct)
        ?? throw new NotFoundException(nameof(Expense), expenseId);

    private async Task<bool> SendToUserAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        var tokens = await db.PushTokens.Where(t => t.UserId == userId).Select(t => t.Token).ToListAsync(ct);
        if (tokens.Count == 0)
        {
            logger.LogInformation("User {UserId} has no registered device; notification skipped.", userId);
            return false;
        }

        NotificationResult result;
        try
        {
            result = await notifications.SendAsync(tokens, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed push is logged and reported as NOT sent. The decision itself is already saved.
            logger.LogWarning(ex, "Push notification to user {UserId} failed.", userId);
            return false;
        }

        if (result.InvalidTokens.Count > 0)
        {
            var dead = await db.PushTokens.Where(t => result.InvalidTokens.Contains(t.Token)).ToListAsync(ct);
            db.PushTokens.RemoveRange(dead);
            await db.SaveChangesAsync(ct);
        }
        return result.Sent > 0;
    }

    private static string Money(Expense e) =>
        $"{e.Amount.ToString("0.00", CultureInfo.InvariantCulture)} {e.Currency}";

    private static string Truncate(string? text) =>
        text is null ? "" : text.Length <= MaxReasonInNotification ? text : text[..MaxReasonInNotification] + "…";

    private static Dictionary<string, string> Data(Expense e) => new() { ["expenseId"] = e.Id.ToString() };
}
