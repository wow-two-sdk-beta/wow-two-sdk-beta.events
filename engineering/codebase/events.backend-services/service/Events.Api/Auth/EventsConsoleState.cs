namespace Events.Api.Auth;

/// <summary>Records whether the host composed the review console's sign-in, so the pipeline and the endpoints follow the
/// same decision the services took.</summary>
/// <param name="IsEnabled">Whether <c>Events:Console</c> named an authority and a client id.</param>
internal sealed record EventsConsoleState(bool IsEnabled);
