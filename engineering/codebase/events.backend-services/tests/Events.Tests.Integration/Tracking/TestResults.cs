using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Tracking;

/// <summary>Reads a broker's result in a test: its value, or its error.</summary>
internal static class TestResults
{
    /// <summary>Asserts success and returns the value.</summary>
    public static T Value<T>(this Result<T> result)
        where T : notnull =>
        result.Should().BeOfType<Result<T>.Success>($"the call should succeed, but failed with {(result as Result<T>.Failure)?.Error.Message}").Which.Value;

    /// <summary>Asserts failure and returns the error.</summary>
    public static AppError Error<T>(this Result<T> result)
        where T : notnull =>
        result.Should().BeOfType<Result<T>.Failure>().Which.Error;
}
