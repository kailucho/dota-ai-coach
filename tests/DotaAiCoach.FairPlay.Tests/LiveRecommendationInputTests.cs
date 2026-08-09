using System.Collections;
using System.Reflection;
using DotaAiCoach.FairPlay;
using DotaAiCoach.FeatureEngine;
using DotaAiCoach.RecommendationEngines;
using FluentAssertions;

namespace DotaAiCoach.FairPlay.Tests;

/// <summary>
/// The structural test dota-fairplay's testing guidance calls for: walk
/// the type graph reachable from the live recommendation engine's actual
/// entry point and assert every reachable Domain/FeatureEngine type is
/// either a BCL primitive/collection or explicitly classified as one of
/// the live-allowed FairPlayClassification values. A future type sourced
/// from Replay/ExternalApi/ComputerVision that somehow became reachable
/// from here would fail this test at build time, not be caught only by
/// review.
/// </summary>
public class LiveRecommendationInputTests
{
    [Fact]
    public void RuleRecommendationEngine_Evaluate_input_type_implements_the_live_marker_interface()
    {
        var method = typeof(RuleRecommendationEngine).GetMethod(nameof(RuleRecommendationEngine.Evaluate))!;
        var inputType = method.GetParameters().Single().ParameterType;

        typeof(ILiveRecommendationInput).IsAssignableFrom(inputType).Should().BeTrue(
            $"{inputType.Name} is the live recommendation engine's input and must implement ILiveRecommendationInput");
    }

    [Fact]
    public void RuleRecommendationEngine_Evaluate_input_type_is_classified_LiveAllowed()
    {
        var method = typeof(RuleRecommendationEngine).GetMethod(nameof(RuleRecommendationEngine.Evaluate))!;
        var inputType = method.GetParameters().Single().ParameterType;

        var attribute = inputType.GetCustomAttribute<FairPlaySourceAttribute>();
        attribute.Should().NotBeNull();
        FairPlayPolicy.IsAllowedLive(attribute!.Classification).Should().BeTrue();
    }

    [Fact]
    public void Every_type_reachable_from_LiveFeatureSet_is_either_a_bcl_type_or_LiveAllowed_classified()
    {
        var visited = new HashSet<Type>();
        var offenders = new List<string>();

        Walk(typeof(LiveFeatureSet), visited, offenders);

        offenders.Should().BeEmpty(
            "every type reachable from the live recommendation input must be a BCL/primitive type or explicitly classified live-allowed");
    }

    [Fact]
    public void RecommendationEngines_assembly_does_not_reference_Gsi()
    {
        // Structural guard against a wire type ever being smuggled in as
        // a rule's input, even indirectly.
        var refs = typeof(RuleRecommendationEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        refs.Should().NotContain("DotaAiCoach.Gsi");
    }

    private static void Walk(Type type, HashSet<Type> visited, List<string> offenders)
    {
        if (type.IsGenericParameter || !visited.Add(type))
        {
            return;
        }

        if (IsBclOrPrimitive(type))
        {
            return;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                Walk(arg, visited, offenders);
            }
            return;
        }

        if (type.Namespace is null || !type.Namespace.StartsWith("DotaAiCoach", StringComparison.Ordinal))
        {
            return;
        }

        var attribute = type.GetCustomAttribute<FairPlaySourceAttribute>();
        if (attribute is null || !FairPlayPolicy.IsAllowedLive(attribute.Classification))
        {
            // Enums without a FairPlaySourceAttribute are pure vocabulary
            // (GameState, ReasonCode, MessagePriority) with no data-origin
            // question of their own — only their containing type's
            // classification matters. Skip them rather than flagging.
            if (!type.IsEnum)
            {
                offenders.Add(type.FullName ?? type.Name);
            }
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Walk(property.PropertyType, visited, offenders);
        }
    }

    private static bool IsBclOrPrimitive(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
            type == typeof(Guid) || type == typeof(void))
        {
            return true;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return IsBclOrPrimitive(underlying);
        }

        return type.Namespace is not null &&
               (type.Namespace.StartsWith("System", StringComparison.Ordinal));
    }
}
