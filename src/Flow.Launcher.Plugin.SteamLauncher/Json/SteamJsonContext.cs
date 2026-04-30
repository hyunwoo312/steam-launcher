using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.SteamLauncher.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(StoreSearchResponse))]
[JsonSerializable(typeof(OwnedGamesResponse))]
[JsonSerializable(typeof(PlayerSummariesResponse))]
[JsonSerializable(typeof(SteamLevelResponse))]
[JsonSerializable(typeof(RecentlyPlayedResponse))]
[JsonSerializable(typeof(AppReviewsResponse))]
[JsonSerializable(typeof(Dictionary<string, AppDetailsEnvelope>))]
[JsonSerializable(typeof(GetFriendListResponse))]
internal sealed partial class SteamJsonContext : JsonSerializerContext
{
}

public sealed record AppReviewsResponse(
    [property: JsonPropertyName("success")] int? Success,
    [property: JsonPropertyName("query_summary")] AppReviewsSummary? QuerySummary);

public sealed record AppReviewsSummary(
    [property: JsonPropertyName("review_score_desc")] string? ReviewScoreDesc,
    [property: JsonPropertyName("total_positive")] int? TotalPositive,
    [property: JsonPropertyName("total_negative")] int? TotalNegative,
    [property: JsonPropertyName("total_reviews")] int? TotalReviews);

public sealed record AppDetailsEnvelope(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] AppDetailsData? Data);

public sealed record AppDetailsData(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("is_free")] bool? IsFree,
    [property: JsonPropertyName("developers")] List<string>? Developers,
    [property: JsonPropertyName("release_date")] AppReleaseDate? ReleaseDate,
    [property: JsonPropertyName("categories")] List<AppDetailsCategory>? Categories);

public sealed record AppDetailsCategory(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("description")] string? Description);

public sealed record AppReleaseDate(
    [property: JsonPropertyName("coming_soon")] bool? ComingSoon,
    [property: JsonPropertyName("date")] string? Date);

public sealed record HealthCheckResponse(
    [property: JsonPropertyName("response")] HealthCheckBody? Response);

public sealed record HealthCheckBody(
    [property: JsonPropertyName("server_time")] long? ServerTime);

public sealed record StoreSearchResponse(
    [property: JsonPropertyName("items")] List<StoreSearchItem>? Items,
    [property: JsonPropertyName("total")] int? Total);

public sealed record StoreSearchItem(
    [property: JsonPropertyName("id")] uint Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("tiny_image")] string? TinyImage,
    // Storefront returns metascore as a string ("82") or empty string, not an integer.
    [property: JsonPropertyName("metascore")] string? Metascore,
    [property: JsonPropertyName("price")] StoreSearchPrice? Price,
    [property: JsonPropertyName("platforms")] StoreSearchPlatforms? Platforms,
    [property: JsonPropertyName("controller_support")] string? ControllerSupport,
    [property: JsonPropertyName("released")] string? Released);

public sealed record StoreSearchPrice(
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("initial")] long? InitialCents,
    [property: JsonPropertyName("final")] long? FinalCents,
    [property: JsonPropertyName("discount_percent")] int? DiscountPercent);

public sealed record StoreSearchPlatforms(
    [property: JsonPropertyName("windows")] bool? Windows,
    [property: JsonPropertyName("mac")] bool? Mac,
    [property: JsonPropertyName("linux")] bool? Linux);

public sealed record OwnedGamesResponse(
    [property: JsonPropertyName("response")] OwnedGamesBody? Response);

public sealed record OwnedGamesBody(
    [property: JsonPropertyName("game_count")] int? GameCount,
    [property: JsonPropertyName("games")] List<OwnedGame>? Games);

public sealed record OwnedGame(
    [property: JsonPropertyName("appid")] uint AppId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("playtime_forever")] int PlaytimeForever,
    [property: JsonPropertyName("playtime_2weeks")] int? Playtime2Weeks,
    [property: JsonPropertyName("rtime_last_played")] long? RtimeLastPlayed);

public sealed record PlayerSummariesResponse(
    [property: JsonPropertyName("response")] PlayerSummariesBody? Response);

public sealed record PlayerSummariesBody(
    [property: JsonPropertyName("players")] List<PlayerSummary>? Players);

public sealed record PlayerSummary(
    [property: JsonPropertyName("steamid")] string? SteamId,
    [property: JsonPropertyName("personaname")] string? PersonaName,
    [property: JsonPropertyName("avatarfull")] string? AvatarUrl,
    [property: JsonPropertyName("personastate")] int? PersonaState,
    [property: JsonPropertyName("gameextrainfo")] string? GameExtraInfo,
    [property: JsonPropertyName("gameid")] string? GameId,
    [property: JsonPropertyName("lastlogoff")] long? LastLogoff);

public sealed record SteamLevelResponse(
    [property: JsonPropertyName("response")] SteamLevelBody? Response);

public sealed record SteamLevelBody(
    [property: JsonPropertyName("player_level")] int? PlayerLevel);

public sealed record RecentlyPlayedResponse(
    [property: JsonPropertyName("response")] RecentlyPlayedBody? Response);

public sealed record RecentlyPlayedBody(
    [property: JsonPropertyName("total_count")] int? TotalCount,
    [property: JsonPropertyName("games")] List<OwnedGame>? Games);

public sealed record GetFriendListResponse(
    [property: JsonPropertyName("friendslist")] FriendListBody? FriendsList);

public sealed record FriendListBody(
    [property: JsonPropertyName("friends")] List<FriendListEntry>? Friends);

public sealed record FriendListEntry(
    [property: JsonPropertyName("steamid")] string? SteamId,
    [property: JsonPropertyName("relationship")] string? Relationship,
    [property: JsonPropertyName("friend_since")] long? FriendSince);
