using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SurrealDB.Abstractions;
using SurrealDB.Driver.Rest;
using SurrealDB.Driver.Rpc;
using SurrealDB.Extensions.Service;
using SurrealDB.Shared.Tests;

namespace SurrealDB.Core.Tests;

public class ServicesTests {
    [Fact]
    public void BuildRpcService() {
        var services = new ServiceCollection();

        services.AddSurrealDB(static b => b
           .WithEndpoint("127.0.0.1:8082")
           .WithDatabase("test")
           .WithNamespace("test")
           .WithRpc());

        var serviceProvider = services.BuildServiceProvider();

        var test = serviceProvider.GetRequiredService<SurrealOptions>();

        Assert.NotNull(serviceProvider.GetService<IDatabase>());
        Assert.NotNull(serviceProvider.GetService<IDatabase<RpcResponse>>());
        Assert.NotNull(serviceProvider.GetService<DatabaseRpc>());
    }

    [Fact]
    public void BuildRestService() {
        var services = new ServiceCollection();

        services.AddSurrealDB(static b => b
           .WithEndpoint("127.0.0.1:8082")
           .WithDatabase("test")
           .WithNamespace("test")
           .WithRest());

        var serviceProvider = services.BuildServiceProvider();

        var test = serviceProvider.GetRequiredService<SurrealOptions>();

        Assert.NotNull(serviceProvider.GetService<IDatabase>());
        Assert.NotNull(serviceProvider.GetService<IDatabase<RestResponse>>());
        Assert.NotNull(serviceProvider.GetService<DatabaseRest>());
    }
}
