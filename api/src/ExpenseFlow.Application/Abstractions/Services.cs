using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Application.Abstractions;

/// <summary>Current time. Faked in tests so results are predictable. Always UTC.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>The logged-in user, read from the JWT by the API.</summary>
public interface ICurrentUser
{
    Guid UserId { get; } // throws if not authenticated
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public record AuthToken(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AuthToken CreateToken(User user);
}

public record ReceiptUploadTicket(string BlobName, Uri UploadUrl, DateTime ExpiresAtUtc);

/// <summary>Azure Blob Storage (Azurite locally). Implemented in Infrastructure.</summary>
public interface IReceiptStorage
{
    /// <summary>Write-only SAS URL for one new blob, generated server-side under receipts/{userId}/.</summary>
    ReceiptUploadTicket CreateUploadTicket(Guid userId);

    /// <summary>Short-lived read-only SAS URL to display a receipt.</summary>
    Uri CreateReadUrl(string blobName);

    /// <summary>Used before saving an expense: a failed upload must never become a valid expense.</summary>
    Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken);
}

/// <summary>Elsa Workflows. Implemented in Infrastructure.</summary>
public interface IWorkflowService
{
    /// <summary>Starts the approval workflow and returns its instance id.</summary>
    Task<string> StartApprovalAsync(Guid expenseId, CancellationToken cancellationToken);

    /// <summary>Resumes the waiting workflow after the manager's decision.</summary>
    Task ResumeAfterDecisionAsync(string workflowInstanceId, Guid expenseId, CancellationToken cancellationToken);
}

public record NotificationMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

/// <param name="Sent">Number of notifications accepted by Expo.</param>
/// <param name="InvalidTokens">Tokens Expo reported as dead (DeviceNotRegistered), to delete.</param>
public record NotificationResult(int Sent, IReadOnlyCollection<string> InvalidTokens);

/// <summary>HTTP client to the Node notification service. Implemented in Infrastructure.</summary>
public interface INotificationClient
{
    Task<NotificationResult> SendAsync(IReadOnlyCollection<string> tokens, NotificationMessage message,
        CancellationToken cancellationToken);
}
