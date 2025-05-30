using System.Text.Json.Serialization;

namespace Elsa.Workflows.Activities.Flowchart.Models;

/// <summary>
/// Represents an endpoint of a connection.
/// </summary>
public class Endpoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Endpoint"/> class.
    /// </summary>
    [JsonConstructor]
    public Endpoint()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Endpoint"/> class.
    /// </summary>
    /// <param name="activity">The activity that the endpoint is connected to.</param>
    /// <param name="port">The port that the endpoint is connected to.</param>
    public Endpoint(IActivity activity, string? port = null)
    {
        Activity = activity;
        Port = port;
    }
    
    /// <summary>
    /// The activity that the endpoint is connected to.
    /// </summary>
    public IActivity Activity { get; set; } = null!;
    
    /// <summary>
    /// The port that the endpoint is connected to.
    /// </summary>
    public string? Port { get; set; }
}