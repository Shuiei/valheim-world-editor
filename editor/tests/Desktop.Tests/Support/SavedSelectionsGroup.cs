using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Runs the saved selections' tests one at a time: they share the store's path.
/// </summary>
[CollectionDefinition("Saved selections", DisableParallelization = true)]
public sealed class SavedSelectionsGroup
{
}
