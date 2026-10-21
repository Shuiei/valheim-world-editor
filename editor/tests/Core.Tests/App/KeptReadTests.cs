using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A value worked out once per source (KeptRead): kept while the source is the same, worked out again for another,
/// and not kept when the work throws.
/// </summary>
public class KeptReadTests
{
    [Fact]
    public void TheValueIsWorkedOutOncePerSource()
    {
        int reads = 0;
        var kept = new KeptRead<object, string>(source => $"{source} {++reads}");
        object a = "a", b = "b";
        Assert.False(kept.TryKept(a, out _));
        Assert.Equal("a 1", kept.Of(a));
        Assert.Equal("a 1", kept.Of(a));
        Assert.True(kept.TryKept(a, out string? value));
        Assert.Equal("a 1", value);
        // Another source: worked out again, the old one dropped.
        Assert.False(kept.TryKept(b, out _));
        Assert.Equal("b 2", kept.Of(b));
        Assert.False(kept.TryKept(a, out _));
        Assert.Equal("a 3", kept.Of(a));
    }

    [Fact]
    public void WorkThatThrowsIsNotKept()
    {
        int reads = 0;
        var kept = new KeptRead<object, string>(_ => ++reads == 1 ? throw new IOException("busy") : "read");
        object a = new();
        Assert.Throws<IOException>(() => kept.Of(a));
        Assert.False(kept.TryKept(a, out _));
        Assert.Equal("read", kept.Of(a));
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task CallersAtTheSameTimeWaitForTheSameWork()
    {
        int reads = 0;
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var entered = new ManualResetEventSlim();
        using var go = new ManualResetEventSlim();
        var kept = new KeptRead<object, string>(_ =>
        {
            Interlocked.Increment(ref reads);
            entered.Set();
            go.Wait(ct);
            return "read";
        });
        object a = new();
        var first = Task.Run(() => kept.Of(a), ct);
        entered.Wait(ct);
        // The second caller comes while the first read is held, and the work is let go only once it is blocked:
        // waiting for that read (or in a read of its own, which the count catches).
        Thread? secondThread = null;
        var second = Task.Factory.StartNew(() =>
        {
            Volatile.Write(ref secondThread, Thread.CurrentThread);
            return kept.Of(a);
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref secondThread) is { } t && (t.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
        go.Set();
        Assert.Equal(["read", "read"], await Task.WhenAll(first, second));
        Assert.Equal(1, reads);
    }
}
