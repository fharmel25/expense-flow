using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Features.Devices;

// ---------- Register (after login) ----------

public record RegisterPushTokenCommand(string Token, DevicePlatform Platform) : IRequest;

public sealed class RegisterPushTokenValidator : AbstractValidator<RegisterPushTokenCommand>
{
    public RegisterPushTokenValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200)
            .Must(PushToken.IsValidExpoToken).WithMessage("Invalid Expo push token.");
        RuleFor(x => x.Platform).IsInEnum();
    }
}

public sealed class RegisterPushTokenHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<RegisterPushTokenCommand>
{
    public async Task Handle(RegisterPushTokenCommand cmd, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var existing = await db.PushTokens.FirstOrDefaultAsync(t => t.Token == cmd.Token, ct);

        if (existing is null)
            db.PushTokens.Add(new PushToken(userId, cmd.Token, cmd.Platform, clock.UtcNow));
        else
            existing.Touch(userId, clock.UtcNow); // same phone: update it, never a duplicate row

        await db.SaveChangesAsync(ct);
    }
}

// ---------- Unregister (on logout) ----------

/// <summary>On logout the phone must stop receiving the previous user's notifications.</summary>
public record UnregisterPushTokenCommand(string Token) : IRequest;

public sealed class UnregisterPushTokenHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UnregisterPushTokenCommand>
{
    public async Task Handle(UnregisterPushTokenCommand cmd, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        // Only your own token can be removed. Unknown or foreign tokens are ignored (logout always succeeds).
        var token = await db.PushTokens.FirstOrDefaultAsync(t => t.Token == cmd.Token && t.UserId == userId, ct);
        if (token is null) return;

        db.PushTokens.Remove(token);
        await db.SaveChangesAsync(ct);
    }
}
