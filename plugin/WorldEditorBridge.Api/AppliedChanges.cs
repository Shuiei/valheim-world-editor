using System.Collections.Generic;

namespace WorldEditorBridge.Api;

/// <summary>What the editor applied to the game in one call (see <see cref="IBridgeListener"/>).</summary>
public sealed class AppliedChanges
{
    /// <summary>One call's changes.</summary>
    /// <param name="created">The objects made.</param>
    /// <param name="destroyed">The objects destroyed (those found again under another id included).</param>
    /// <param name="terrainZones">The zones whose ground was rewritten.</param>
    /// <param name="resetZones">The zones emptied to be generated again.</param>
    public AppliedChanges(IReadOnlyList<AppliedObject> created, IReadOnlyList<AppliedObject> destroyed, IReadOnlyList<Zone> terrainZones, IReadOnlyList<Zone> resetZones)
    {
        Created = created;
        Destroyed = destroyed;
        TerrainZones = terrainZones;
        ResetZones = resetZones;
    }

    /// <summary>The objects made: find them with ZDOMan.instance.GetZDO(new ZDOID(user, id)).</summary>
    public IReadOnlyList<AppliedObject> Created { get; }

    /// <summary>The objects destroyed: their ids are gone, their prefab and place say what they were.</summary>
    public IReadOnlyList<AppliedObject> Destroyed { get; }

    /// <summary>The zones whose ground (TerrainComp data) was rewritten.</summary>
    public IReadOnlyList<Zone> TerrainZones { get; }

    /// <summary>The zones emptied, to be generated again when a player comes.</summary>
    public IReadOnlyList<Zone> ResetZones { get; }
}

/// <summary>An object the editor made or destroyed.</summary>
public sealed class AppliedObject
{
    /// <summary>An object.</summary>
    /// <param name="user">The user part of its ZDOID.</param>
    /// <param name="id">The id part of its ZDOID.</param>
    /// <param name="prefab">Its prefab's hash (ZNetScene.instance.GetPrefab names it).</param>
    /// <param name="x">Its position's x, in metres.</param>
    /// <param name="y">Its position's y, in metres.</param>
    /// <param name="z">Its position's z, in metres.</param>
    public AppliedObject(long user, uint id, int prefab, float x, float y, float z)
    {
        User = user;
        Id = id;
        Prefab = prefab;
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The user part of its ZDOID.</summary>
    public long User { get; }

    /// <summary>The id part of its ZDOID.</summary>
    public uint Id { get; }

    /// <summary>Its prefab's hash.</summary>
    public int Prefab { get; }

    /// <summary>Its position's x, in metres.</summary>
    public float X { get; }

    /// <summary>Its position's y, in metres.</summary>
    public float Y { get; }

    /// <summary>Its position's z, in metres.</summary>
    public float Z { get; }
}

/// <summary>A zone of the world (64 m squares; ZoneSystem.GetZone).</summary>
public sealed class Zone
{
    /// <summary>A zone.</summary>
    /// <param name="x">Its x.</param>
    /// <param name="z">Its z.</param>
    public Zone(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>Its x.</summary>
    public int X { get; }

    /// <summary>Its z.</summary>
    public int Z { get; }
}
