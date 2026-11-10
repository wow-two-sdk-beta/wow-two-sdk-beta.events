using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Catalogs;

/// <summary>Reads a broker's failure in assertions.</summary>
internal static class TestResults
{
    /// <summary>Gets the error of a failed result, failing the test when it succeeded.</summary>
    public static AppError ErrorOf<T>(Result<T> result) where T : notnull =>
        result.Should().BeOfType<Result<T>.Failure>().Which.Error;
}
