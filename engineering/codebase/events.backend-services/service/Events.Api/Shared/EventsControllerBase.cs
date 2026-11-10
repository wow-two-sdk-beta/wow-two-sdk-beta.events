using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace Events.Api.Shared;

/// <summary>Answers the HTTP mirror's way: <c>{ "data": … }</c> on success, the error's problem body and status
/// (contract § HTTP mirror) on failure.</summary>
[ApiController]
public abstract class EventsControllerBase : ControllerBase
{
    /// <summary>Answers a broker's result with <c>200</c>.</summary>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="result">The broker's result.</param>
    /// <returns>The envelope, or the problem.</returns>
    protected IActionResult Answer<T>(Result<T> result) where T : notnull =>
        result.Match<IActionResult>(value => Ok(ApiResponse<T>.Ok(value)), error => this.ToProblem(error));

    /// <summary>Answers a broker's result with <c>201</c> and the created resource's location.</summary>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="result">The broker's result.</param>
    /// <param name="location">Maps the created value to its route.</param>
    /// <returns>The envelope, or the problem.</returns>
    protected IActionResult AnswerCreated<T>(Result<T> result, Func<T, string> location) where T : notnull =>
        result.Match<IActionResult>(value => Created(location(value), ApiResponse<T>.Ok(value)), error => this.ToProblem(error));

    /// <summary>Answers a malformed request with the problem of its error, before any broker runs.</summary>
    /// <param name="error">The validation error.</param>
    /// <returns>The problem.</returns>
    protected IActionResult Invalid(AppError error) => this.ToProblem(error);
}
