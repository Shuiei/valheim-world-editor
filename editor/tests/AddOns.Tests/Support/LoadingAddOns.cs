using Xunit;

namespace WorldEditor.Tests;

/// <summary>The tests that load add-ons, one at a time: the editor's manifests and extensions are shared.</summary>
[CollectionDefinition("AddOns", DisableParallelization = true)]
public sealed class LoadingAddOns;
