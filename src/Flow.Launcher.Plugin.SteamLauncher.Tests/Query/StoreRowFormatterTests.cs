using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Models;
using Flow.Launcher.Plugin.SteamLauncher.Query;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Query;

public sealed class StoreRowFormatterTests
{
    private static StoreGame Game(decimal? price = 19.99m, bool owned = false, string? currency = null) => new()
    {
        AppId = 730,
        Name = "CS2",
        Price = price,
        Currency = currency,
        IsOwned = owned
    };

    [Fact]
    public void Subtitle_NullCurrency_RendersDollarPrefix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(price: 14.99m), GameMetadata.Empty);

        subtitle.Should().StartWith("$14.99");
    }

    [Fact]
    public void Subtitle_EuroCurrency_RendersEuroPrefix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(price: 14.99m, currency: "EUR"), GameMetadata.Empty);

        subtitle.Should().StartWith("€14.99");
    }

    [Fact]
    public void Subtitle_UnknownCurrency_RendersAmountWithCode()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(price: 49.90m, currency: "PLN"), GameMetadata.Empty);

        subtitle.Should().StartWith("49.90 PLN");
    }

    [Fact]
    public void Subtitle_NoFriendsPlaying_OmitsSuffix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty);

        subtitle.Should().NotContain("playing");
        subtitle.Should().NotContain("friend");
    }

    [Fact]
    public void Subtitle_OneFriendPlaying_AppendsSingularSuffix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty, friendsPlaying: 1);

        subtitle.Should().EndWith("· 1 friend playing");
    }

    [Fact]
    public void Subtitle_MultipleFriendsPlaying_AppendsPluralSuffix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(), GameMetadata.Empty, friendsPlaying: 5);

        subtitle.Should().EndWith("· 5 friends playing");
    }

    [Fact]
    public void Subtitle_OwnedNotInstalled_StillAppendsFriendsPlayingSuffix()
    {
        var subtitle = StoreRowFormatter.BuildSubtitle(Game(owned: true), GameMetadata.Empty, friendsPlaying: 2);

        subtitle.Should().EndWith("· 2 friends playing");
    }
}
