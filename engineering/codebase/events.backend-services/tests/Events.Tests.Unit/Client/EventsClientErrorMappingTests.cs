using System.Net;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Unit.Client;

/// <summary>The client maps a failed answer back to the contract's error by its status: 400 Validation, 401 Unauthorized,
/// 403 Forbidden, 404 NotFound, 409 Conflict, anything else and an unreachable service ExternalUnavailable.</summary>
public sealed class EventsClientErrorMappingTests : IDisposable
{
    private readonly StubEventsService service = new();

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, AppErrorType.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, AppErrorType.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AppErrorType.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, AppErrorType.NotFound)]
    [InlineData(HttpStatusCode.Conflict, AppErrorType.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests, AppErrorType.ExternalUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, AppErrorType.ExternalUnavailable)]
    [InlineData(HttpStatusCode.BadGateway, AppErrorType.ExternalUnavailable)]
    public async Task Failure_ShouldMapToTheContractsError_ByStatus(HttpStatusCode status, AppErrorType expected)
    {
        service.Answer(status, $$"""{"type":"urn:wow-two:error:x","status":{{(int)status}},"detail":"From the service.","code":"Unexpected"}""");

        var error = Error(await service.Broker<IEventCatalogsBroker>().GetAsync("fashion"));

        error.Type.Should().Be(expected);
        error.Message.Should().Be("From the service.");
    }

    [Fact]
    public async Task Failure_ShouldMapByStatus_WhenTheBodyIsNoProblem()
    {
        service.Answer(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var error = Error(await service.Broker<IEventCatalogsBroker>().ListAsync());

        error.Type.Should().Be(AppErrorType.Unauthorized);
        error.Message.Should().Contain("401");
    }

    [Fact]
    public async Task Failure_ShouldBeExternalUnavailable_WhenTheServiceIsUnreachable()
    {
        service.Answer(_ => throw new HttpRequestException("connection refused"));

        Error(await service.Broker<IEventCatalogsBroker>().ListAsync()).Type.Should().Be(AppErrorType.ExternalUnavailable);
    }

    [Fact]
    public async Task Success_WithoutData_ShouldFailSerialization()
    {
        service.Answer(HttpStatusCode.OK, "{}");

        Error(await service.Broker<IEventCatalogsBroker>().GetAsync("fashion")).Type.Should().Be(AppErrorType.SerializationFailed);
    }

    public void Dispose() => service.Dispose();

    private static AppError Error<T>(Result<T> result) where T : notnull =>
        result.Should().BeOfType<Result<T>.Failure>().Subject.Error;
}
