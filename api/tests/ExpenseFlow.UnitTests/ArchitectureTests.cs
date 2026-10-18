using ExpenseFlow.Domain.Entities;
using NetArchTest.Rules;

namespace ExpenseFlow.UnitTests;

/// <summary>CLAUDE.md rule: Clean Architecture – dependencies point inward.</summary>
public class ArchitectureTests
{
    private const string ApplicationNs = "ExpenseFlow.Application";
    private const string InfrastructureNs = "ExpenseFlow.Infrastructure";
    private const string ApiNs = "ExpenseFlow.Api";

    [Fact]
    public void Domain_HasNoDependencyOnOtherLayersOrEfCore()
    {
        var result = Types.InAssembly(typeof(Expense).Assembly)
            .ShouldNot().HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(ExpenseFlow.Application.DependencyInjection).Assembly)
            .ShouldNot().HaveDependencyOnAny(InfrastructureNs, ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    private static string FailingTypes(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
