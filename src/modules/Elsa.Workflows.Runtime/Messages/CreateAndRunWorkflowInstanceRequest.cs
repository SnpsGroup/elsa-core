using Elsa.Workflows.Models;
using JetBrains.Annotations;

namespace Elsa.Workflows.Runtime.Messages;

/// <summary>
/// A request to create and run a new workflow instance.
/// </summary>
[UsedImplicitly]
public class CreateAndRunWorkflowInstanceRequest
{
    /// <summary>
    /// The ID of the workflow definition version to create an instance of.
    /// </summary>
    public WorkflowDefinitionHandle WorkflowDefinitionHandle { get; set; } = null!;

    /// <summary>
    /// The correlation ID of the workflow, if any.
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// The name of the workflow instance to be created.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The input to the workflow instance, if any.
    /// </summary>
    public IDictionary<string, object>? Input { get; set; }

    /// <summary>
    /// A collection of custom variables used within the workflow instance.
    /// </summary>
    public IDictionary<string, object>? Variables { get; set; }

    /// <summary>
    /// Any properties to assign to the workflow instance.
    /// </summary>
    public IDictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// The ID of the parent workflow instance, if any.
    /// </summary>
    public string? ParentId { get; set; }
    
    /// <summary>
    /// The ID of the activity that triggered the workflow instance, if any.
    /// </summary>
    public string? TriggerActivityId { get; set; }
    
    /// <summary>
    /// The handle of the activity to schedule, if any.
    /// </summary>
    public ActivityHandle? ActivityHandle { get; set; }

    /// <summary>
    /// When set to <c>true</c>, include workflow output in the response.
    /// </summary>
    public bool IncludeWorkflowOutput { get; set; }
}