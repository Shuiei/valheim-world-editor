using System;
using System.Collections.Generic;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>How many /objects answers are kept by their call id (<see cref="ApplyObjectsOnce"/>).</summary>
    private const int CallsKept = 16;

    /// <summary>The last /objects answers by their call id, oldest first; only read and changed on the main thread.</summary>
    private readonly List<(string Id, byte[] Answer)> _calls = [];

    /// <summary>
    /// This run of the plugin (a new one each time the game starts), in /status: <see cref="_calls"/> is lost with the
    /// game, so an editor whose call's answer was lost does not send it again to another run, which would do it twice.
    /// </summary>
    private readonly string _run = Guid.NewGuid().ToString("N");

    /// <summary>
    /// POST /objects, done once per call id (the X-Call-Id header, from editors 1.45.3 on): a call sent again because
    /// its answer was lost on the way (a dropped tunnel, the editor giving up waiting) is answered with what was done
    /// the first time instead of being done twice, which would make every new object twice. A call without an id is
    /// done each time, as before.
    /// </summary>
    /// <param name="call">The call's id; null when the editor sent none.</param>
    /// <param name="body">The call's body (<see cref="ApplyObjects"/>).</param>
    /// <returns>The JSON answer.</returns>
    private byte[] ApplyObjectsOnce(string call, byte[] body)
    {
        if (call != null)
        {
            foreach (var (id, answer) in _calls)
            {
                if (id == call)
                {
                    Logger.LogInfo("WorldEditorBridge: objects call sent again (its answer was lost): answered with what was done, not done twice");
                    return answer;
                }
            }
        }
        byte[] json = Applied(ApplyObjects(body));
        if (call != null)
        {
            if (_calls.Count == CallsKept)
            {
                _calls.RemoveAt(0);
            }
            _calls.Add((call, json));
        }
        return json;
    }
}
