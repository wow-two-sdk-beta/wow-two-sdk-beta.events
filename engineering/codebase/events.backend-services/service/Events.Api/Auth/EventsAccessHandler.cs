using Microsoft.AspNetCore.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Auth;

/// <summary>Checks <see cref="EventsAccessRequirement"/> against the one principal, so no policy branches by scheme.</summary>
/// <param name="principal">Who is calling.</param>
internal sealed class EventsAccessHandler(ICurrentPrincipal principal) : AuthorizationHandler<EventsAccessRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EventsAccessRequirement requirement)
    {
        if (requirement.Allows(principal))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
