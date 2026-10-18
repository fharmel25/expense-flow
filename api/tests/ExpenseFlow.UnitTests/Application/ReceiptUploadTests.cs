using ExpenseFlow.Application.Abstractions;
using ExpenseFlow.Application.Features.Receipts;
using NSubstitute;

namespace ExpenseFlow.UnitTests.Application;

public class ReceiptUploadTests : IDisposable
{
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task CreateUploadUrl_IsIssuedForTheCurrentUserOnly()
    {
        var ticket = new ReceiptUploadTicket($"receipts/{_ctx.Employee.Id}/abc.jpg",
            new Uri("https://storage.test/upload?sas"), AppTestContext.Now.AddMinutes(5));
        _ctx.Receipts.CreateUploadTicket(_ctx.Employee.Id).Returns(ticket);

        var result = await new CreateReceiptUploadUrlHandler(_ctx.Receipts, _ctx.CurrentUser)
            .Handle(new CreateReceiptUploadUrlCommand(), default);

        Assert.Same(ticket, result);
        _ctx.Receipts.Received(1).CreateUploadTicket(_ctx.Employee.Id);
    }
}
