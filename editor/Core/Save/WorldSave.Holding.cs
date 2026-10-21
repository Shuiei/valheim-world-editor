
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Keeps the world's lists and object bytes as they are until the hold is disposed: <see cref="MergeLive"/>, the
    /// only change to them once loaded (<see cref="SetLiveId"/> changes an object's ZDOID alone), returns null meanwhile
    /// (tried again later). For a reader on another thread that must see the world as it was when it was asked for
    /// (the history written after Apply live).
    /// </summary>
    public IDisposable Hold()
    {
        lock (Sync)
        {
            _holds++;
        }
        return new Held(this);
    }

    /// <summary>A <see cref="Hold"/> taken; let go once, at the first dispose.</summary>
    /// <param name="world">The world held.</param>
    private sealed class Held(WorldSave world) : IDisposable
    {
        /// <summary>1 once let go.</summary>
        private int _done;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                lock (world.Sync)
                {
                    world._holds--;
                }
            }
        }
    }
}
