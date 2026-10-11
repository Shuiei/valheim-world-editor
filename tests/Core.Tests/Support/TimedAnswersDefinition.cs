using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The "Timed answers" collection: tests that wait a few seconds at most for a server on this computer run alone, so
/// the CPU-heavy tests (world generation on every core) cannot hold the threads that answer back past the wait.
/// </summary>
[CollectionDefinition("Timed answers", DisableParallelization = true)]
public class TimedAnswersDefinition
{
}
