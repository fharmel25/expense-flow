using ExpenseFlow.Application.Common;
using ExpenseFlow.Application.Features.Expenses;

namespace ExpenseFlow.UnitTests.Application;

public class ExpenseQueriesTests : IDisposable
{
    private readonly AppTestContext _ctx = new();
    public void Dispose() => _ctx.Dispose();

    private GetExpenseByIdHandler ById() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Receipts);

    [Fact]
    public async Task GetById_Owner_GetsDetail_WithReceiptUrlAndHistory()
    {
        var e = _ctx.AddExpense(_ctx.Employee);

        var dto = await ById().Handle(new GetExpenseByIdQuery(e.Id), default);

        Assert.Equal(e.Id, dto.Id);
        Assert.NotNull(dto.ReceiptUrl);
        Assert.Equal(2, dto.History.Count); // Submitted + InReview
    }

    [Fact]
    public async Task GetById_TeamManager_GetsDetail()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.Manager);

        Assert.Equal(e.Id, (await ById().Handle(new GetExpenseByIdQuery(e.Id), default)).Id);
    }

    [Fact]
    public async Task GetById_OtherEmployee_IsForbidden()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.OtherEmployee);

        await Assert.ThrowsAsync<ForbiddenException>(() => ById().Handle(new GetExpenseByIdQuery(e.Id), default));
    }

    [Fact]
    public async Task GetById_OtherTeamManager_IsForbidden()
    {
        var e = _ctx.AddExpense(_ctx.Employee);
        _ctx.LoginAs(_ctx.OtherManager);

        await Assert.ThrowsAsync<ForbiddenException>(() => ById().Handle(new GetExpenseByIdQuery(e.Id), default));
    }

    [Fact]
    public async Task GetById_Unknown_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => ById().Handle(new GetExpenseByIdQuery(Guid.NewGuid()), default));

    [Fact]
    public async Task GetMine_ReturnsOnlyMyExpenses()
    {
        var mine = _ctx.AddExpense(_ctx.Employee);
        _ctx.AddExpense(_ctx.OtherEmployee);

        var result = await new GetMyExpensesHandler(_ctx.Db, _ctx.CurrentUser).Handle(new GetMyExpensesQuery(), default);

        Assert.Equal(mine.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetPending_ReturnsOnlyOwnTeamInReview()
    {
        var pending = _ctx.AddExpense(_ctx.Employee);
        _ctx.AddExpense(_ctx.Employee, inReview: false); // still Submitted
        _ctx.AddExpense(_ctx.OtherEmployee);              // other team
        _ctx.LoginAs(_ctx.Manager);

        var result = await new GetPendingExpensesHandler(_ctx.Db, _ctx.CurrentUser).Handle(new GetPendingExpensesQuery(), default);

        Assert.Equal(pending.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetPending_NonManager_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetPendingExpensesHandler(_ctx.Db, _ctx.CurrentUser).Handle(new GetPendingExpensesQuery(), default));
}
