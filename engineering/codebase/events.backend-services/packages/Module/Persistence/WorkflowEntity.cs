namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_workflows</c>: one organization's workflow.</summary>
internal sealed class WorkflowEntity
{
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Stages { get; set; } = "[]";
    public string DefaultStage { get; set; } = string.Empty;
    public string? Fields { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
