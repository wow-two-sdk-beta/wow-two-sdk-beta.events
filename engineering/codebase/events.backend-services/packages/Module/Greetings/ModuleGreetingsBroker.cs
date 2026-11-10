using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Events.Greetings;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Greetings;

/// <summary>Integrates the in-process greetings module: composes the greeting in the consumer's process.</summary>
internal sealed class ModuleGreetingsBroker(TimeProvider time) : IGreetingsBroker
{
    /// <inheritdoc />
    public Task<Result<GreetingModel>> GreetAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (name is { Length: > GreetingConstants.MaxNameLength })
            return Task.FromResult(Result<GreetingModel>.Fail(GreetingErrors.NameTooLong()));

        return Task.FromResult(Result<GreetingModel>.Ok(GreetingComposer.Compose(name, time.GetUtcNow())));
    }
}
