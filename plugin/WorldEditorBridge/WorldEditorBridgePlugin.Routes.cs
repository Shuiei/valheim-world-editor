using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Runs <paramref name="work"/> on Unity's main thread and waits for it. Given up on after the timeout, a job
    /// never runs later (the editor, told it failed, sends it again: objects would be made twice); one the main
    /// thread already took is waited for, as it is quick once started.
    /// </summary>
    /// <param name="work">Reads or changes the game; returns the response body.</param>
    /// <param name="timeoutMs">Milliseconds to wait before giving up on a job the main thread has not taken yet.</param>
    /// <returns>What <paramref name="work"/> returned; what it threw is thrown here, and a
    /// <see cref="TimeoutException"/> when the game did not take the job in time.</returns>
    private byte[] OnMainThread(Func<byte[]> work, int timeoutMs = 30000)
    {
        Job job = new() { Work = work };
        _jobs.Enqueue(job);
        // Given up on, a job never runs later (the editor, told it failed, sends it again: objects would be
        // made twice). One the main thread already took is waited for: it is quick once started.
        if (!job.Done.Wait(timeoutMs))
        {
            if (Interlocked.CompareExchange(ref job.State, 2, 0) == 0)
            {
                throw new TimeoutException("the game did not answer in time");
            }
            job.Done.Wait();
        }
        if (job.Error != null)
        {
            // With the stack trace it had on the main thread; the request's catch logs it.
            ExceptionDispatchInfo.Capture(job.Error).Throw();
        }
        return job.Result;
    }

    /// <summary>The request's path, answered (<see cref="Handle"/>): unknown paths get 404.</summary>
    /// <param name="ctx">The request; its body is read where the route takes one.</param>
    /// <param name="path">The request's path, other than a mod's route.</param>
    private void Route(HttpListenerContext ctx, string path)
    {
        HttpListenerResponse res = ctx.Response;
        switch (path)
        {
            case "/mods":
                Reply(res, 200, "application/json", Encoding.UTF8.GetBytes(Mods()));
                break;
            case "/status":
                ReplyJson(res, Status);
                break;
            case "/players":
                ReplyJson(res, Players);
                break;
            case "/catalogue":
                ReplyJson(res, Catalogue, 120000);
                break;
            case "/terrain":
                PostJson(ctx, body => Applied(ApplyTerrain(body)));
                break;
            case "/objects":
            {
                string call = ctx.Request.Headers["X-Call-Id"];
                PostJson(ctx, body => ApplyObjectsOnce(call, body));
                break;
            }
            case "/models":
                PostJson(ctx, Models, 120000);
                break;
            case "/zones/reset":
                PostJson(ctx, body => Applied(ResetZones(body)));
                break;
            case "/watch":
                PostJson(ctx, Watch);
                break;
            case "/zone":
                PostGzip(ctx, ZoneObjects);
                break;
            case "/snapshot":
                ReplyGzip(res, Snapshot, 120000);
                break;
            default:
                Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("unknown path"));
                break;
        }
    }
}
