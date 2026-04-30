using System.Net;
using System.Net.Http;

namespace Flow.Launcher.Plugin.SteamLauncher.Steam;

public sealed class SteamHttpRetryHandler(int maxAttempts = 3, TimeSpan? baseDelay = null)
    : DelegatingHandler
{
    private readonly TimeSpan _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(250);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 0)
            {
                var delay = lastResponse is not null
                    ? GetRetryDelay(lastResponse, attempt)
                    : ExponentialBackoff(attempt);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                lastResponse?.Dispose();
                lastResponse = null;
            }

            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!IsRetryable(response.StatusCode)) return response;
                lastResponse = response;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
        }

        if (lastResponse is not null) return lastResponse;
        throw lastException ?? new HttpRequestException("Retry exhausted with no response");
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests
        || ((int)status >= 500 && (int)status < 600);

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta) return delta;
        return ExponentialBackoff(attempt);
    }

    private TimeSpan ExponentialBackoff(int attempt)
    {
        var jitterMs = Random.Shared.Next(0, 100);
        return _baseDelay * Math.Pow(2, attempt - 1) + TimeSpan.FromMilliseconds(jitterMs);
    }
}
