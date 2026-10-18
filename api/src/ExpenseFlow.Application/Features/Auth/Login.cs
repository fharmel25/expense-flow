using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Features.Auth;

public record UserDto(Guid Id, string Email, string FullName, UserRole Role, Guid? ManagerId)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.FullName, u.Role, u.ManagerId);
}

public record LoginResult(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class LoginHandler(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens)
    : IRequestHandler<LoginCommand, LoginResult>
{
    // Same message for "unknown email" and "wrong password": an attacker cannot learn which emails exist.
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<LoginResult> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !hasher.Verify(user.PasswordHash, cmd.Password))
            throw new UnauthorizedException(InvalidCredentials);

        var token = tokens.CreateToken(user);
        return new LoginResult(token.AccessToken, token.ExpiresAtUtc, UserDto.From(user));
    }
}
