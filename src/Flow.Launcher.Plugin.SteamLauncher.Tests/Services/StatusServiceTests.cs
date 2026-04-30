using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class StatusServiceTests
{
    [Theory]
    [InlineData(PersonaStateDesired.Online,    "steam://friends/status/online")]
    [InlineData(PersonaStateDesired.Offline,   "steam://friends/status/offline")]
    [InlineData(PersonaStateDesired.Invisible, "steam://friends/status/invisible")]
    public void Set_LaunchesMatchingSteamUri(PersonaStateDesired desired, string expectedUri)
    {
        string? launched = null;
        var service = new StatusService(uri => launched = uri);

        service.Set(desired);

        launched.Should().Be(expectedUri);
    }

    [Fact]
    public void Set_PropagatesLaunchExceptions()
    {
        var service = new StatusService(_ => throw new InvalidOperationException("URI handler missing"));

        var act = () => service.Set(PersonaStateDesired.Online);

        act.Should().Throw<InvalidOperationException>().WithMessage("URI handler missing");
    }
}
