using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Shared.Tests;

public static class TestHelper {

    public const string Loopback = "127.0.0.1";
    public const int Port = 23458;
    public const string User = "root";
    public const string Pass = "root";
    public const string Database = "test";
    public const string Namespace = "test";

    public static Config Default => Config.Create()
       .WithAddress(Loopback)
       .WithPort(Port)
       .WithNamespace(Namespace)
       .WithDatabase(Database)
       .WithRpc(true)
       .WithRest(true)
       .WithBasicAuth(User, Pass)
       .Build();

    public static void ValidateEndpoint(IPEndPoint? endpoint) {
        endpoint.Should().NotBeNull();
        endpoint!.Address.ToString().Should().Be(Loopback);
        endpoint.Port.Should().Be(Port);
    }

    public static void AssertOk(
        DriverResponse rsp,
        [CallerArgumentExpression("rsp")]
        string caller = "") {
        if (!rsp.HasErrors) {
            return;
        }

        var errorResponses = rsp.Errors.ToList();
        var message = $"Expected OK, got {errorResponses.Count} Error responses in {caller}";
        foreach (var errorResponse in errorResponses) {
            message += $"\n\tStatus: {errorResponse.Status} | Message: {errorResponse.Message}";
        }

        Exception ex = new(message);
        throw ex;
    }

    public static void AssertError(
        in DriverResponse rsp,
        [CallerArgumentExpression("rsp")]
        string caller = "") {
        if (rsp.HasErrors) {
            return;
        }

        var errorResponses = rsp.Errors.ToList();
        var message = $"Expected Error, got {errorResponses.Count} OK responses in {caller}";

        Exception ex = new(message);
        throw ex;
    }
}
