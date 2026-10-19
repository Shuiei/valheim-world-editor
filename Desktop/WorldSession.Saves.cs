using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// After a save that needed the world read again: reads the save back (or says it could not be read), prunes the
    /// earlier saves, and starts the session over from the world as it was.
    /// </summary>
    /// <param name="result">The writer's result, with the save it made.</param>
    /// <returns>What could not be read back as one line, or <see cref="Said.Empty"/> when all of it was.</returns>
    internal Said ReadBackSave(WorldWriter.Result result)
    {
        Said unread = Said.Empty;
        // Written: whatever happens reading it back, what went in is not pending any more (it would be written
        // twice). Not read back, the session starts over from the world as it was and says to open it again.
        try
        {
            // The save as read back to check it: not read a third time. The world's rules stay what they were worked
            // out to be when it was opened.
            var read = result.Read ?? WorldSave.Load(World.Directory);
            read.Rules = World.Rules;
            World = read;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Caught(ex, "reading the save back to check it");
            unread = Said.Of("save.msg.notReadBack", ex.Message);
        }
        if (unread == Said.Empty)
        {
            try
            {
                WorldWriter.Prune(World);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Log.Caught(ex, "removing the world's earlier saves after saving");
            }
        }
        Edits.ResetFrom(World);
        UseModifiers(new TerrainModifiers(World));
        _nextId = -1;
        ForgetHistory();
        ForgetSaves();
        if (unread != Said.Empty)
        {
            // The session's own save is not one the game made since.
            _lastNumber = result.Number;
        }
        return unread;
    }

    /// <summary>
    /// Offline: the number of a save the game made after the editor read the world (or last saved it), which a save
    /// would throw away (<see cref="Save"/> refuses); null when there is none.
    /// </summary>
    public int? GameSaveSince()
    {
        int known = _lastNumber ?? World.SaveNumber;
        return !IsLive && Directory.Exists(World.Directory) && WorldSave.CommittedSave(World.Directory) is int newest && newest > known ? newest : null;
    }

    /// <summary>Leaving an offline world it saved: the save it was opened from goes (only the latest stays).</summary>
    private void PruneSaves()
    {
        if (!IsLive && _lastSave != null && Directory.Exists(World.Directory))
        {
            // The world is left whatever happens: a folder that cannot be read now keeps its files.
            try
            {
                WorldWriter.Prune(World.Directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Log.Caught(ex, "removing the world's earlier saves on leaving it");
            }
        }
        ForgetSaves();
    }
}
