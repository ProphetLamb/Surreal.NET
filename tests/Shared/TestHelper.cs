namespace SurrealDB.Shared.Tests;

public static class TestHelper {
    
    public const string Loopback = "127.0.0.1";
    public const int Port = 8082;
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
        in IResponse rpcResponse,
        // [CallerArgumentExpression("rpcResponse")]
        string caller = "") {
        if (!rpcResponse.TryGetError(out Error err)) {
            return;
        }

        Exception ex = new($"Expected Ok, got error code {err.Code} ({err.Message}) in {caller}");
        throw ex;
    }

    public static void AssertError(
        in IResponse rpcResponse,
        // [CallerArgumentExpression("rpcResponse")]
        string caller = "") {
        if (rpcResponse.TryGetError(out Error err)) {
            return;
        }

        Exception ex = new($"Expected Error, got ok response in {caller}");
        throw ex;
    }
}
