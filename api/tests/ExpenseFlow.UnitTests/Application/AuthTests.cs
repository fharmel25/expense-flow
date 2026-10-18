using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Common;
using ExpenseFlow.Application.Features.Auth;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

public class AuthTests : IDisposable
{
    private const string GoodPassword = "Passw0rd!";
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    public AuthTests()
    {
        _ctx.Hasher.Verify("hash", GoodPassword).Returns(true); // every test user has PasswordHash = "hash"
        _ctx.Tokens.CreateToken(Arg.Any<User>()).Returns(new AuthToken("jwt-token", AppTestContext.Now.AddHours(1)));
    }

    private LoginHandler Login() => new(_ctx.Db, _ctx.Hasher, _ctx.Tokens);

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndUser_EmailIsCaseInsensitive()
    {
        var result = await Login().Handle(new LoginCommand("  Employee@Test.com ", GoodPassword), default);

        Assert.Equal("jwt-token", result.AccessToken);
        Assert.Equal(_ctx.Employee.Id, result.User.Id);
        Assert.Equal(UserRole.Employee, result.User.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_IsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Login().Handle(new LoginCommand("employee@test.com", "wrong"), default));

    [Fact]
    public async Task Login_UnknownEmail_IsUnauthorized_WithTheSameMessage()
    {
        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Login().Handle(new LoginCommand("nobody@test.com", GoodPassword), default));
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Login().Handle(new LoginCommand("employee@test.com", "wrong"), default));

        Assert.Equal(wrongPassword.Message, unknown.Message); // no hint about which emails exist
    }

    [Theory]
    [InlineData("", GoodPassword)]
    [InlineData("not-an-email", GoodPassword)]
    [InlineData("employee@test.com", "")]
    public void LoginValidator_InvalidInput_Fails(string email, string password) =>
        Assert.False(new LoginValidator().Validate(new LoginCommand(email, password)).IsValid);

    [Fact]
    public async Task GetMe_ReturnsTheCurrentUser()
    {
        _ctx.LoginAs(_ctx.Manager);

        var me = await new GetMeHandler(_ctx.Db, _ctx.CurrentUser).Handle(new GetMeQuery(), default);

        Assert.Equal(_ctx.Manager.Id, me.Id);
        Assert.Equal(UserRole.Manager, me.Role);
    }
}
