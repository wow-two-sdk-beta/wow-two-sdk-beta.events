namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads SCFD's answer envelope: <c>{ "data": … }</c>.</summary>
/// <typeparam name="T">The answer's type.</typeparam>
internal sealed record ScfdEnvelope<T>
{
    /// <summary>Gets the answer.</summary>
    public T? Data { get; init; }
}
