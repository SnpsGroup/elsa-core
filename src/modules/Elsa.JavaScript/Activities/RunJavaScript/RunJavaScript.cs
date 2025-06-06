﻿using System.Runtime.CompilerServices;
using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.JavaScript.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using Jint;

// ReSharper disable once CheckNamespace
namespace Elsa.JavaScript.Activities;

/// <summary>
/// Executes JavaScript code.
/// </summary>
[Activity("Elsa", "Scripting", "Executes JavaScript code", DisplayName = "Run JavaScript")]
public class RunJavaScript : CodeActivity<object?>
{
    /// <inheritdoc />
    public RunJavaScript([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    public RunJavaScript(string script, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
    {
        Script = new Input<string>(script);
    }

    /// <summary>
    /// The script to run.
    /// </summary>
    [Input(
        Description = "The script to run.",
        DefaultSyntax = "JavaScript",
        UIHint = InputUIHints.CodeEditor,
        UIHandler = typeof(RunJavaScriptOptionsProvider)
    )]
    public Input<string> Script { get; set; } = new("");

    /// <summary>
    /// A list of possible outcomes. Use "setOutcome()" to set the outcome. Use "setOutcomes" to set multiple outcomes.
    /// </summary>
    [Input(Description = "A list of possible outcomes.", UIHint = InputUIHints.DynamicOutcomes)]
    public Input<ICollection<string>> PossibleOutcomes { get; set; } = null!;

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var script = context.Get(Script);

        // If no script was specified, there's nothing to do.
        if (string.IsNullOrWhiteSpace(script))
            return;

        // Get a JavaScript evaluator.
        var javaScriptEvaluator = context.GetRequiredService<IJavaScriptEvaluator>();

        // Run the script.
        var result = await javaScriptEvaluator.EvaluateAsync(
            script,
            typeof(object),
            context.ExpressionExecutionContext,
            ExpressionEvaluatorOptions.Empty,
            engine => ConfigureEngine(engine, context),
            context.CancellationToken);

        // Set the result as output, if any.
        if (result is not null)
            context.Set(Result, result);

        // Get the outcome or outcomes set by the script, if any. If not set, use "Done".
        var outcomes = context.TransientProperties.GetValueOrDefault("Outcomes", () => new[] { "Done" })!;

        // Complete the activity with the outcome.
        await context.CompleteActivityWithOutcomesAsync(outcomes);
    }

    private static void ConfigureEngine(Engine engine, ActivityExecutionContext context)
    {
        engine.SetValue("setOutcome", (Action<string>)(value => context.TransientProperties["Outcomes"] = new[] { value }));
        engine.SetValue("setOutcomes", (Action<string[]>)(value => context.TransientProperties["Outcomes"] = value));
    }
}