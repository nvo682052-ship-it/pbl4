using System.Net;
using System.Net.Http.Headers;
using pbl4.Networking;
using Xunit;

namespace pbl4.Networking.Tests;

public sealed class HttpRangeClientTests
{
    [Fact]
    public async Task DownloadRangeAsync_SendsApplicationUserAgent()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var rangeClient = new HttpRangeClient(httpClient);

        await rangeClient.DownloadRangeAsync(
            new Uri("https://example.test/file.bin"),
            0,
            1,
            new MemoryStream());

        Assert.Equal("pbl4-download-accelerator/1.0", handler.UserAgent?.ToString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ProductInfoHeaderValue? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.FirstOrDefault();
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([1, 2])
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 1, 2);
            return Task.FromResult(response);
        }
    }
}
