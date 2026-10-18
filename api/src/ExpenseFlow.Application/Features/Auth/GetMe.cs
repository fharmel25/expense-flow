using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Application.Features.Auth;

/// <summary>Who am I? Used by the app at startup to restore the session and show the right tabs.</summary>
public record GetMeQuery : IRequest<UserDto>;

public sealed class GetMeHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetMeQuery, UserDto>
{
    public async Task<UserDto> Handle(GetMeQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);
        return UserDto.From(user);
    }
}
