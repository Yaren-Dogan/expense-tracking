using System.Reflection;
using ExpenseTracker.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>
/// Every endpoint that calls the chat or embedding model draws on the provider quota all users share, so each one must
/// carry the per-user AI limit. The suite raises the limit so it cannot throttle itself; this checks the wiring instead.
/// </summary>
public class RateLimitAttributeTests
{
    public static TheoryData<Type, string> ModelCallingEndpoints => new()
    {
        { typeof(ExpensesController), nameof(ExpensesController.Parse) },
        { typeof(ExpensesController), nameof(ExpensesController.Search) },
        { typeof(AssistantController), nameof(AssistantController.Chat) },
        { typeof(InsightsController), nameof(InsightsController.CreateReview) },
        { typeof(InsightsController), nameof(InsightsController.CreatePlan) }
    };

    [Theory]
    [MemberData(nameof(ModelCallingEndpoints))]
    public void Endpoints_that_call_the_model_use_the_ai_rate_limit(Type controller, string action)
    {
        var attribute = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        attribute.Should().NotBeNull($"{controller.Name}.{action} calls the model");
        attribute!.PolicyName.Should().Be(RateLimitPolicies.Ai);
    }
}
