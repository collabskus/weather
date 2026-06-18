using System.Net;

namespace Weather.Infrastructure.Tests.Fakes;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> whose response is produced by a
/// caller-supplied delegate. It records every request (including headers) so
/// tests can assert that, for example, a conditional <c>If-None-Match</c> was
/// sent. Set <see cref="ThrowOnSend"/> to simulate a transport failure.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public HttpRequestMessage? LastRequest => _requests.Count > 0 ? _requests[^1] : null;

    public Exception? ThrowOnSend { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (ThrowOnSend is not null)
        {
            return Task.FromException<HttpResponseMessage>(ThrowOnSend);
        }

        var response = responder(request);
        return Task.FromResult(response);
    }

    /// <summary>Build a JSON (application/geo+json, as NWS uses) response.</summary>
    public static HttpResponseMessage GeoJson(HttpStatusCode statusCode, string body)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json");
        return response;
    }
}
