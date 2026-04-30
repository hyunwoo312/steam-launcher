using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Cache;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Cache;

public sealed class CachePoliciesTests
{
    [Fact]
    public void DefaultPolicies_HaveUniqueDomains()
    {
        var domains = CachePolicies.Default.Select(p => p.Domain).ToList();
        domains.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DefaultPolicies_AllHavePositiveTtls()
    {
        foreach (var policy in CachePolicies.Default)
        {
            policy.SuccessTtl.Should().BePositive($"success TTL for '{policy.Domain}'");
            policy.FailureTtl.Should().BePositive($"failure TTL for '{policy.Domain}'");
        }
    }

    [Fact]
    public void DefaultPolicies_CoverAllNamedDomainConstants()
    {
        var declaredDomains = new[]
        {
            CachePolicies.Search, CachePolicies.PlayerCount, CachePolicies.ReviewScore,
            CachePolicies.AppDetails, CachePolicies.OwnedGames, CachePolicies.FriendList,
            CachePolicies.PlayerSummaries
        };
        var policyDomains = CachePolicies.Default.Select(p => p.Domain).ToHashSet();

        policyDomains.Should().BeEquivalentTo(declaredDomains);
    }
}
