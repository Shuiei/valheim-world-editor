using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The "DataDir" collection: tests that point the app's data folder, settings or servers file elsewhere never run in
/// parallel.
/// </summary>
[CollectionDefinition("DataDir", DisableParallelization = true)]
public class DataDirDefinition
{
}
