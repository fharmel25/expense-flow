using ExpenseFlow.Domain.Common;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities;

public class PushToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DevicePlatform Platform { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    private PushToken() { } // required by EF Core

    public PushToken(Guid userId, string token, DevicePlatform platform, DateTime utcNow)
    {
        UtcGuard.Ensure(utcNow);
        if (!IsValidExpoToken(token)) throw new DomainException("Invalid Expo push token.");
        Id = Guid.NewGuid();
        UserId = userId;
        Token = token;
        Platform = platform;
        LastSeenAt = utcNow;
    }

    /// <summary>Same device seen again (possibly another user logged in on it).</summary>
    public void Touch(Guid userId, DateTime utcNow)
    {
        UtcGuard.Ensure(utcNow);
        UserId = userId;
        LastSeenAt = utcNow;
    }

    public static bool IsValidExpoToken(string? token) =>
        !string.IsNullOrWhiteSpace(token)
        && (token.StartsWith("ExponentPushToken[") || token.StartsWith("ExpoPushToken["))
        && token.EndsWith("]");
}
