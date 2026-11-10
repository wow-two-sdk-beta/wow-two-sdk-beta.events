namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Runs just before a page's transaction commits; a test seam proving that a crash there commits nothing.</summary>
internal interface IIntakeCommitHook
{
    /// <summary>Runs inside the page's transaction, after every write and before the commit; throwing rolls it back.</summary>
    /// <param name="source">The source whose page is committing.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task BeforeCommitAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken);
}
