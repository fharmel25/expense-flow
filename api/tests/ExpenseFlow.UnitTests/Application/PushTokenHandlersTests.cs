using ExpenseFlow.Application.Features.Devices;
using ExpenseFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.UnitTests.Application;

public class PushTokenHandlersTests : IDisposable
{
    private const string Token = "ExpoPushToken[phone-1]";
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    private RegisterPushTokenHandler Register() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Clock);
    private UnregisterPushTokenHandler Unregister() => new(_ctx.Db, _ctx.CurrentUser);

    [Fact]
    public async Task Register_NewToken_IsSavedForCurrentUser()
    {
        await Register().Handle(new RegisterPushTokenCommand(Token, DevicePlatform.Android), default);

        var saved = await _ctx.Db.PushTokens.SingleAsync();
        Assert.Equal(_ctx.Employee.Id, saved.UserId);
    }

    [Fact]
    public async Task Register_SameTokenTwice_DoesNotCreateDuplicate()
    {
        await Register().Handle(new RegisterPushTokenCommand(Token, DevicePlatform.Android), default);
        _ctx.Db.ChangeTracker.Clear();
        await Register().Handle(new RegisterPushTokenCommand(Token, DevicePlatform.Android), default);

        Assert.Equal(1, await _ctx.Db.PushTokens.CountAsync());
    }

    [Fact]
    public async Task Register_PhoneUsedByAnotherUserBefore_MovesTokenToCurrentUser()
    {
        _ctx.AddPushToken(_ctx.OtherEmployee, Token); // someone else logged in on this phone before

        await Register().Handle(new RegisterPushTokenCommand(Token, DevicePlatform.Android), default);

        var saved = await _ctx.Db.PushTokens.SingleAsync();
        Assert.Equal(_ctx.Employee.Id, saved.UserId); // the previous user no longer gets this phone's pushes
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void RegisterValidator_InvalidToken_Fails(string token) =>
        Assert.False(new RegisterPushTokenValidator()
            .Validate(new RegisterPushTokenCommand(token, DevicePlatform.Android)).IsValid);

    [Fact]
    public async Task Unregister_OwnToken_IsDeleted()
    {
        _ctx.AddPushToken(_ctx.Employee, Token);

        await Unregister().Handle(new UnregisterPushTokenCommand(Token), default);

        Assert.Equal(0, await _ctx.Db.PushTokens.CountAsync());
    }

    [Fact]
    public async Task Unregister_AnotherUsersToken_IsNotDeleted()
    {
        _ctx.AddPushToken(_ctx.OtherEmployee, Token);

        await Unregister().Handle(new UnregisterPushTokenCommand(Token), default);

        Assert.Equal(1, await _ctx.Db.PushTokens.CountAsync());
    }
}
