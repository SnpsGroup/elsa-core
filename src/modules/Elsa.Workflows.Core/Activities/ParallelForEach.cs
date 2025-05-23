using System.Runtime.CompilerServices;
using Elsa.Workflows.Attributes;

namespace Elsa.Workflows.Activities;

/// <summary>
/// Schedule an activity for each item in parallel.
/// </summary>
[Activity("Elsa", "Looping", "Schedule an activity for each item in parallel.")]
public class ParallelForEach : ParallelForEach<object>
{
    /// <inheritdoc />
    public ParallelForEach([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }
}