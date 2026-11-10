using System.Collections.Concurrent;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Integration.Intake;

/// <summary>Crashes an armed source's page just before its transaction commits.</summary>
internal sealed class CrashingCommitHook : IIntakeCommitHook
{
    private readonly ConcurrentDictionary<string, bool> armed = new(StringComparer.Ordinal);

    /// <summary>Arms or disarms the crash for a source.</summary>
    public void Arm(EventsIntakeSourceOptions source, bool on = true)
    {
        if (on)
            armed[source.Name] = true;
        else
            armed.TryRemove(source.Name, out _);
    }

    /// <inheritdoc />
    public Task BeforeCommitAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken) =>
        armed.ContainsKey(source.Name) ? throw new InvalidOperationException("Simulated crash before the cursor commit.") : Task.CompletedTask;
}
