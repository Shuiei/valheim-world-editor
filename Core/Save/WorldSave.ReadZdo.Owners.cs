
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Takes a long entry: the creation time, the creator, the owner or the player id.</summary>
    /// <param name="f">The fields being filled.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private static void ReadLongField(ZdoFields f, ZdoReader zdo, int key)
    {
        if (key == TimeCreatedKey)
        {
            f.TimeCreated = zdo.LongValue;
        }
        else if (key == Editing.ObjectField.CreatorKey)
        {
            f.Creator = zdo.LongValue;
        }
        else if (key == OwnerKey)
        {
            f.Owner = zdo.LongValue;
        }
        else if (key == PlayerIdKey)
        {
            f.PlayerId = zdo.LongValue;
        }
    }
    /// <summary>Takes a string entry: the name of the owner, creator or player when its id was read.</summary>
    /// <param name="f">The fields being read.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="key">The entry's key.</param>
    private void ReadStringField(ZdoFields f, ZdoReader zdo, int key)
    {
        long who = key == OwnerNameKey ? f.Owner : key == CreatorNameKey ? f.Creator : key == PlayerNameKey ? f.PlayerId : 0;
        if (who != 0 && zdo.StringValue!.Length > 0)
        {
            PlayerNames[who] = zdo.StringValue;
        }
    }
}
