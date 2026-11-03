using System.Collections.Concurrent;
using System.Net;
using pbl4.Networking;

namespace pbl4.Integration.Tests.Fakes;

internal sealed class FakeHttpRangeClient : IHttpRangeClient
{
    private readonly byte[] _content;
    private readonly bool _supportsRanges;
    private readonly Func<long, long, CancellationToken, Task>? _beforeDownload;

    public FakeHttpRangeClient(
        byte[] content,
        bool supportsRanges = true,
        Func<long, long, CancellationToken, Task>? beforeDownload = null)
    {
        _content = content;
        _supportsRanges = supportsRanges;
        _beforeDownload = beforeDownload;
    }

    private readonly ConcurrentQueue<(long Start, long End)> _requestedRanges = new();

    public IReadOnlyCollection<(long Start, long End)> RequestedRanges => _requestedRanges.ToArray();

    public Task<HttpRangeProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken = default)
        => Task.FromResult(new HttpRangeProbeResult(
            url,
            _supportsRanges,
            _content.Length,
            ETag: "\"fake\"",
            LastModified: DateTimeOffset.UtcNow,
            ContentType: "application/octet-stream",
            HttpVersion: new Version(1, 1)));

    public async Task<HttpRangeResponse> DownloadRangeAsync(
        Uri url,
        long start,
        long end,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        _requestedRanges.Enqueue((start, end));
        if (_beforeDownload is not null)
        {
            await _beforeDownload(start, end, cancellationToken);
        }

        var length = (int)(end - start + 1);
        await destination.WriteAsync(_content.AsMemory((int)start, length), cancellationToken);

        return new HttpRangeResponse(
            HttpStatusCode.PartialContent,
            start,
            end,
            start,
            end,
            _content.Length,
            length,
            IsPartialContent: true);
    }
}
