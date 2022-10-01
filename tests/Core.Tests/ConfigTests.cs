using SurrealDB.Shared.Tests;

namespace SurrealDB.Core.Tests;

public class ConfigTests {
    [Fact]
    public void Build_with_endpoint() {
        Config cfg = Config.Create()
           .WithEndpoint($"{TestHelper.Loopback}:{TestHelper.Port}")
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }
    [Fact]
    public void Build_with_endpoint_Not_Chained() {
        var cfgBuilder = Config.Create();
        cfgBuilder.WithEndpoint($"{TestHelper.Loopback}:{TestHelper.Port}");
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }

    [Fact]
    public void Build_with_address_and_port() {
        Config cfg = Config.Create()
           .WithAddress(TestHelper.Loopback)
           .WithPort(TestHelper.Port)
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }

    [Fact]
    public void Build_with_address_and_port_Not_Chained() {
        var cfgBuilder = Config.Create();
        cfgBuilder.WithAddress(TestHelper.Loopback);
        cfgBuilder.WithPort(TestHelper.Port);
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }

    [Fact]
    public void Build_last_option_should_overwrite_prior() {
        Config cfg = Config.Create()
           .WithEndpoint($"0.0.0.0:{TestHelper.Port}")
           .WithAddress(TestHelper.Loopback)
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }

    [Fact]
    public void Build_last_option_should_overwrite_prior_Not_Chained() {
        var cfgBuilder = Config.Create();
        cfgBuilder.WithEndpoint($"0.0.0.0:{TestHelper.Port}");
        cfgBuilder.WithAddress(TestHelper.Loopback);
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
    }

    [Fact]
    public void Build_with_Rpc() {
        Config cfg = Config.Create()
           .WithAddress(TestHelper.Loopback).WithPort(TestHelper.Port)
           .WithRpc()
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.RpcEndpoint.Should().NotBeNull();
        cfg.RestEndpoint.Should().BeNull();
    }

    [Fact]
    public void Build_with_Rpc_Not_Chained() {
        var cfgBuilder = Config.Create();
        cfgBuilder.WithAddress(TestHelper.Loopback);
        cfgBuilder.WithPort(TestHelper.Port);
        cfgBuilder.WithRpc();
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.RpcEndpoint.Should().NotBeNull();
        cfg.RestEndpoint.Should().BeNull();
    }

    [Fact]
    public void Build_with_Rest() {
        Config cfg = Config.Create()
           .WithAddress(TestHelper.Loopback)
           .WithPort(TestHelper.Port)
           .WithRest()
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.RestEndpoint.Should().NotBeNull();
        cfg.RpcEndpoint.Should().BeNull();
    }

    [Fact]
    public void Build_with_Rest_Not_Chained() {
        var cfgBuilder = Config.Create();
        cfgBuilder.WithAddress(TestHelper.Loopback);
        cfgBuilder.WithPort(TestHelper.Port);
        cfgBuilder.WithRest();
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.RestEndpoint.Should().NotBeNull();
        cfg.RpcEndpoint.Should().BeNull();
    }

    [Fact]
    public void Build_with_Basic_Auth() {
        var username = "TestUsername";
        var password = "TestPassword";

        Config cfg = Config.Create()
           .WithAddress(TestHelper.Loopback)
           .WithPort(TestHelper.Port)
           .WithBasicAuth(username, password)
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.Username.Should().Be(username);
        cfg.Password.Should().Be(password);
    }

    [Fact]
    public void Build_with_Basic_Auth_Not_Chained() {
        var username = "TestUsername";
        var password = "TestPassword";

        var cfgBuilder = Config.Create();
        cfgBuilder.WithAddress(TestHelper.Loopback);
        cfgBuilder.WithPort(TestHelper.Port);
        cfgBuilder.WithBasicAuth(username, password);
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.Username.Should().Be(username);
        cfg.Password.Should().Be(password);
    }

    [Fact]
    public void Build_with_Jwt_Auth() {
        var jwtToken = "a.jwt.token";

        Config cfg = Config.Create()
           .WithAddress(TestHelper.Loopback)
           .WithPort(TestHelper.Port)
           .WithJwtAuth(jwtToken)
           .Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.JsonWebToken.Should().Be(jwtToken);
    }

    [Fact]
    public void Build_with_Jwt_Auth_Not_Chained() {
        var jwtToken = "a.jwt.token";

        var cfgBuilder = Config.Create();
        cfgBuilder.WithAddress(TestHelper.Loopback);
        cfgBuilder.WithPort(TestHelper.Port);
        cfgBuilder.WithJwtAuth(jwtToken);
        var cfg = cfgBuilder.Build();

        TestHelper.ValidateEndpoint(cfg.Endpoint);
        cfg.JsonWebToken.Should().Be(jwtToken);
    }
}
