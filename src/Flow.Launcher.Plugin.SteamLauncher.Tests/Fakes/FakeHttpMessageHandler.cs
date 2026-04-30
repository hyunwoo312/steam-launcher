using System.Net;
using System.Net.Http;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Fakes;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    public List<HttpRequestMessage> ReceivedRequests { get; } = new();

    public FakeHttpMessageHandler EnqueueStatus(HttpStatusCode status, string? body = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        _responses.Enqueue(_ =>
        {
            var msg = new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? string.Empty)
            };
            if (headers is not null)
                foreach (var (k, v) in headers) msg.Headers.TryAddWithoutValidation(k, v);
            return msg;
        });
        return this;
    }

    public FakeHttpMessageHandler EnqueueException(Exception ex)
    {
        _responses.Enqueue(_ => throw ex);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedRequests.Add(request);
        if (_responses.Count == 0)
            throw new InvalidOperationException("No response queued");
        return Task.FromResult(_responses.Dequeue()(request));
    }
}
