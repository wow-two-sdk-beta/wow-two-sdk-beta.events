using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events;

/// <summary>Holds the error catalogue of the Events contract.</summary>
public static class EventsErrors
{
    /// <summary>Creates the error for a catalog the caller cannot see or that does not exist.</summary>
    public static AppError CatalogNotFound() => AppErrorFactory.NotFound("The catalog was not found.");

    /// <summary>Creates the error for an event the caller cannot see or that does not exist.</summary>
    public static AppError EventNotFound() => AppErrorFactory.NotFound("The event was not found.");

    /// <summary>Creates the error for an observation the caller cannot see or that does not exist.</summary>
    public static AppError ObservationNotFound() => AppErrorFactory.NotFound("The observation was not found.");

    /// <summary>Creates the error for tracking the caller's organization does not hold.</summary>
    public static AppError TrackingNotFound() => AppErrorFactory.NotFound("The tracking was not found.");

    /// <summary>Creates the error for a write whose expected revision is not the current one.</summary>
    public static AppError RevisionConflict() => AppErrorFactory.Conflict("The record changed since it was read; reload and retry.");

    /// <summary>Creates the error for a key or slug already taken in its catalog or instance.</summary>
    public static AppError KeyTaken() => AppErrorFactory.Conflict("The key is already taken.");

    /// <summary>Creates the error for a review action on an event in the wrong review state.</summary>
    /// <param name="expected">The state the action needs.</param>
    public static AppError ReviewStateInvalid(string expected) =>
        AppErrorFactory.Conflict($"The event must be {expected} for this action.");

    /// <summary>Creates the error for a call that needs an active organization and holds none.</summary>
    public static AppError OrganizationRequired() =>
        AppErrorFactory.Forbidden("The call needs an active organization.");

    /// <summary>Creates the error for a curation, review or import by an organization that does not own the catalog.</summary>
    public static AppError NotCatalogOwner() =>
        AppErrorFactory.Forbidden("Only the catalog's owner organization may do this.");

    /// <summary>Creates the error for an invalid field.</summary>
    /// <param name="field">The field's JSON name.</param>
    /// <param name="rule">What the field must be.</param>
    public static AppError Invalid(string field, string rule) => AppErrorFactory.Validation($"{field}: {rule}");

    /// <summary>Creates the error for a list cursor no previous page returned.</summary>
    public static AppError CursorInvalid() => AppErrorFactory.Validation("The list cursor is not valid.");

    /// <summary>Creates the error for a stage the organization's workflow does not hold.</summary>
    /// <param name="stage">The unknown stage.</param>
    public static AppError StageUnknown(string stage) =>
        AppErrorFactory.Validation($"stage: '{stage}' is not a stage of the organization's workflow.");

    /// <summary>Creates the error for an import bundle that fails validation, listing its first failures.</summary>
    /// <param name="failures">The row failures, at most 50.</param>
    public static AppError ImportInvalid(IEnumerable<string> failures) =>
        AppErrorFactory.Validation("The import bundle is invalid: " + string.Join("; ", failures.Take(50)));
}
