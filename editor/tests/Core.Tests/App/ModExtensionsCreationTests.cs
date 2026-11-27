using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The running extensions' creation rules (ModExtensions.Create): each handed the objects of the prefabs it covers,
/// which it may leave out, change or add beside; rules that throw turn their extension off. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsCreationTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>
    /// Rules that swap Beech1 for Oak1 (with a value), mark every Rock_4 and add a vfx beside it, or throw.
    /// </summary>
    private sealed class Rules(bool fail = false, bool failAfterRemove = false) : IWorldEditorExtension, ICreationRules
    {
        public List<string> Handed { get; } = [];

        public void Register(IExtensionRegistry registry) => registry.AddCreationRules(this);

        public bool Covers(string prefab) => prefab is "Beech1" or "Rock_4";

        public void Created(ICreation made)
        {
            if (fail)
            {
                throw new InvalidOperationException("no");
            }
            foreach (var obj in made.Objects)
            {
                Handed.Add(obj.Prefab);
                if (obj.Prefab == "Beech1")
                {
                    made.Remove(obj);
                    if (failAfterRemove)
                    {
                        throw new InvalidOperationException("half way");
                    }
                    made.Add("Oak1", obj.Position, obj.Rotation).SetInt("swapped", 1);
                }
                else
                {
                    obj.SetInt("marked", 7);
                }
            }
        }
    }

    private static ModExtensions.Making M(string prefab, float x = 0) => new(prefab, new Vector3(x, 30, 0), Vector3.Zero, 1);

    private static ModExtensions.Creation? Create(params ModExtensions.Making[] batch) =>
        ModExtensions.Create(batch, _ => [], 1, (_, _) => 1, (_, _) => 30);

    [Fact]
    public void WithoutCreationRulesNothingIsDone()
    {
        Assert.False(ModExtensions.HasCreationRules);
        Assert.Null(Create(M("Beech1")));
    }

    [Fact]
    public void TheRulesGetTheirPrefabsAndLeaveOutChangeAndAdd()
    {
        var rules = new Rules();
        ModExtensions.UseForTests(("Prefabs", rules));

        var done = Create(M("Beech1", 1), M("Bush01", 2), M("Rock_4", 3));

        Assert.NotNull(done);
        Assert.Equal(["Beech1", "Rock_4"], rules.Handed);
        Assert.Equal([0], done.Removed);
        var marked = Assert.Single(done.Changed);
        Assert.Equal(2, marked.Key);
        Assert.Equal(new ObjectField("ints", StableHash.Of("marked"), "7"), Assert.Single(marked.Value.Values));
        var oak = Assert.Single(done.Added);
        Assert.Equal(("Oak1", new Vector3(1, 30, 0)), (oak.Prefab, oak.Position));
    }

    [Fact]
    public void RulesThatThrowTurnTheirExtensionOff()
    {
        ModExtensions.UseForTests(("Broken", new Rules(fail: true)));

        var done = Create(M("Beech1"));

        Assert.True(done is null or { None: true });
        Assert.Contains(ModExtensions.Offered, e => e.Mod == "Broken" && e.State == ModExtensions.Status.Failed);
    }

    [Fact]
    public void WhatRulesThatThrowDidIsDiscarded()
    {
        ModExtensions.UseForTests(("Broken", new Rules(failAfterRemove: true)));

        var done = Create(M("Rock_4", 1), M("Beech1", 2));

        // The rock was marked and the beech removed before the throw: neither stays.
        Assert.True(done is null or { None: true });
    }
}
