using System.Net;
using System.Net.Http;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Tests.Fakes;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Steam;

public sealed class SteamHttpRetryHandlerTests
{
    private static HttpClient CreateClient(FakeHttpMessageHandler fake)
    {
        var retryHandler = new SteamHttpRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.Zero)
        {
            InnerHandler = fake
        };
        return new HttpClient(retryHandler) { BaseAddress = new Uri("https://test/") };
    }

    [Fact]
    public async Task SuccessOnFirstTry_DoesNotRetry()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.OK, "ok");
        var client = CreateClient(fake);

        var response = await client.GetAsync("/x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task TransientServerError_RetriesAndSucceeds()
    {
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.OK, "ok");
        var client = CreateClient(fake);

        var response = await client.GetAsync("/x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.ReceivedRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task FourHundred_DoesNotRetry()
    {
        var fake = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.BadRequest);
        var client = CreateClient(fake);

        var response = await client.GetAsync("/x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fake.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task TooManyRequests_RetriesUpToLimit()
    {
        var fake = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.TooManyRequests)
            .EnqueueStatus(HttpStatusCode.TooManyRequests)
            .EnqueueStatus(HttpStatusCode.TooManyRequests);
        var client = CreateClient(fake);

        var response = await client.GetAsync("/x");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        fake.ReceivedRequests.Should().HaveCount(3);
    }

    [Fact]
    public async Task NetworkException_Retries()
    {
        var fake = new FakeHttpMessageHandler()
            .EnqueueException(new HttpRequestException("dns fail"))
            .EnqueueStatus(HttpStatusCode.OK, "ok");
        var client = CreateClient(fake);

        var response = await client.GetAsync("/x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.ReceivedRequests.Should().HaveCount(2);
    }
}
