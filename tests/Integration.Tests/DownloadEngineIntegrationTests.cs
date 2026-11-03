using pbl4.Core;
using pbl4.Integration.Tests.Fakes;
using pbl4.OSIntegration;
using Xunit;

namespace pbl4.Integration.Tests;

public sealed class DownloadEngineIntegrationTests
{
    [Fact]
    public async Task CreateAsync_WhenServerSupportsRanges_CreatesMultipleSegments()
    {
        var engine = CreateEngine(new byte[16], out _, out var repository);

        var job = await engine.CreateAsync(new CreateDownloadRequest(
            new Uri("https://example.test/file.bin"),
            CreateTempOutputPath(),
            SegmentCount: 4));

        var saved = await repository.FindAsync(job.Id);

        Assert.NotNull(saved);
        Assert.Equal(4, saved.RequestedConcurrency);
        Assert.Equal(16, saved.Segments.Count);
        Assert.All(saved.Segments, segment => Assert.Equal(1, segment.Length));
    }

    [Fact]
    public async Task CreateAsync_WhenServerDoesNotSupportRanges_FallsBackToOneSegment()
    {
        var engine = CreateEngine(new byte[16], out _, out _, supportsRanges: false);

        var job = await engine.CreateAsync(new CreateDownloadRequest(
            new Uri("https://example.test/file.bin"),
            CreateTempOutputPath(),
            SegmentCount: 8));

        Assert.Single(job.Segments);
        Assert.Equal(16, job.Segments[0].Length);
    }

    [Fact]
    public async Task StartAsync_DownloadsAllSegmentsAndMovesPartFileToFinalPath()
    {
        var content = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var engine = CreateEngine(content, out var http, out _);
        var outputPath = CreateTempOutputPath();
        var job = await engine.CreateAsync(new CreateDownloadRequest(
            new Uri("https://example.test/file.bin"),
            outputPath,
            SegmentCount: 4));

        await engine.StartAsync(job.Id);

        var downloaded = await File.ReadAllBytesAsync(outputPath);
        TryDelete(outputPath);

        Assert.Equal(content, downloaded);
        Assert.Equal(16, http.RequestedRanges.Count);
    }

    [Fact]
    public async Task StartAsync_WithExistingSegmentProgress_RequestsOnlyRemainingBytes()
    {
        var content = Enumerable.Range(0, 12).Select(i => (byte)(i + 1)).ToArray();
        var http = new FakeHttpRangeClient(content);
        var repository = new InMemoryDownloadRepository();
        var outputPath = CreateTempOutputPath();
        var job = new DownloadJob(
            Guid.NewGuid(),
            new Uri("https://example.test/file.bin"),
            outputPath,
            12,
            new[]
            {
                new DownloadSegment(0, 0, 5, downloadedBytes: 6, SegmentState.Completed),
                new DownloadSegment(1, 6, 11, downloadedBytes: 2, SegmentState.Pending)
            },
            DownloadState.Pending,
            requestedConcurrency: 2);
        await repository.SaveAsync(job);
        var engine = new DownloadEngine(http, new SegmentPlanner(), repository, new ProgressReporter());

        await engine.StartAsync(job.Id);
        TryDelete(outputPath);

        Assert.Contains(http.RequestedRanges, range => range.Start == 8 && range.End == 11);
        Assert.DoesNotContain(http.RequestedRanges, range => range.Start == 0);
    }

    [Fact]
    public async Task StartAsync_ReusesWorkerForNextChunkWhileAnotherChunkIsStillRunning()
    {
        var slowChunkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterChunkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlowChunk = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var http = new FakeHttpRangeClient(
            new byte[80],
            beforeDownload: async (start, _, cancellationToken) =>
            {
                if (start == 0)
                {
                    slowChunkStarted.TrySetResult();
                    await releaseSlowChunk.Task.WaitAsync(cancellationToken);
                }
                else if (start >= 20)
                {
                    laterChunkStarted.TrySetResult();
                }
            });
        var repository = new InMemoryDownloadRepository();
        var engine = new DownloadEngine(http, new SegmentPlanner(), repository, new ProgressReporter());
        var job = await engine.CreateAsync(new CreateDownloadRequest(
            new Uri("https://example.test/file.bin"),
            CreateTempOutputPath(),
            SegmentCount: 2));

        Assert.Equal(2, job.RequestedConcurrency);
        Assert.Equal(8, job.Segments.Count);

        var download = engine.StartAsync(job.Id);
        try
        {
            await slowChunkStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await laterChunkStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            releaseSlowChunk.TrySetResult();
        }

        await download;
        TryDelete(job.OutputPath);
    }

    private static DownloadEngine CreateEngine(
        byte[] content,
        out FakeHttpRangeClient http,
        out InMemoryDownloadRepository repository,
        bool supportsRanges = true)
    {
        http = new FakeHttpRangeClient(content, supportsRanges);
        repository = new InMemoryDownloadRepository();
        return new DownloadEngine(
            http,
            new SegmentPlanner(),
            repository,
            new ProgressReporter(),
            new DownloadEngineOptions { MaxConcurrency = 8, BufferSize = 4 });
    }

    private static string CreateTempOutputPath()
        => Path.Combine(Path.GetTempPath(), $"pbl4-it-{Guid.NewGuid():N}.bin");

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var part = path + ".part";
        if (File.Exists(part))
        {
            File.Delete(part);
        }
    }
}

