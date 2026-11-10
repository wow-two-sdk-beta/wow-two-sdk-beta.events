namespace WoW.Two.Sdk.Beta.Be.Events.Workflows;

/// <summary>Says what a stage means for reports and deadlines.</summary>
public enum StageClass
{
    /// <summary>Considered, nothing started.</summary>
    Open,

    /// <summary>Work in progress.</summary>
    Active,

    /// <summary>Done, as hoped.</summary>
    Won,

    /// <summary>Dropped; its deadlines leave the summary.</summary>
    Lost,
}
