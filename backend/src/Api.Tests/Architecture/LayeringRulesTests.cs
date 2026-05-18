using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Api.Tests.Architecture;

public sealed class LayeringRulesTests
{
    private static readonly Assembly ApiAssembly = typeof(Api.Contracts.ChatRequest).Assembly;

    [Fact]
    public void Controllers_Should_Have_Controller_Suffix()
    {
        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Api.Controllers")
            .Should()
            .HaveNameEndingWith("Controller")
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(result.FailingTypeNames));
    }

    [Fact]
    public void Services_Should_Not_Depend_On_Controllers()
    {
        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Api.Services")
            .ShouldNot()
            .HaveDependencyOn("Api.Controllers")
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(result.FailingTypeNames));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Controllers()
    {
        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Api.Application")
            .ShouldNot()
            .HaveDependencyOn("Api.Controllers")
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(result.FailingTypeNames));
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Controllers_Or_Application()
    {
        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Api.Contracts")
            .ShouldNot()
            .HaveDependencyOnAny("Api.Controllers", "Api.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(result.FailingTypeNames));
    }

    private static string BuildFailureMessage(IReadOnlyCollection<string> failingTypeNames)
    {
        if (failingTypeNames == null || failingTypeNames.Count == 0)
        {
            return string.Empty;
        }

        return $"Architecture rule violations: {string.Join(", ", failingTypeNames)}";
    }
}
