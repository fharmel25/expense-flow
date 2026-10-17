using ExpenseFlow.Domain.Common;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.UnitTests.Domain;

public class PushTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("ExponentPushToken[abc123]")]
    [InlineData("ExpoPushToken[abc123]")]
    public void Constructor_ValidExpoToken_IsAccepted(string token) =>
        Assert.Equal(token, new PushToken(Guid.NewGuid(), token, DevicePlatform.Android, Now).Token);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ExponentPushToken[abc")]
    public void Constructor_InvalidToken_Throws(string token) =>
        Assert.Throws<DomainException>(() => new PushToken(Guid.NewGuid(), token, DevicePlatform.Android, Now));

    [Fact]
    public void Touch_SameDeviceNewUser_UpdatesOwnerAndLastSeen()
    {
        var token = new PushToken(Guid.NewGuid(), "ExpoPushToken[x]", DevicePlatform.Android, Now);
        var newUser = Guid.NewGuid();
        var later = Now.AddHours(1);

        token.Touch(newUser, later);

        Assert.Equal(newUser, token.UserId);
        Assert.Equal(later, token.LastSeenAt);
    }
}
