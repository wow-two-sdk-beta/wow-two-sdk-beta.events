namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>References a product's own entity from tracking, opaquely: Events never reads it.</summary>
/// <param name="Type">The entity's type, such as <c>ocharo.campaign</c>.</param>
/// <param name="Id">The entity's id in the product.</param>
public sealed record TrackingReference(string Type, string Id);
