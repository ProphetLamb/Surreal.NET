using Microsoft.Extensions.DependencyInjection;

using SurrealDB.Abstractions;
using SurrealDB.Configuration;
using SurrealDB.Driver.Rest;
using SurrealDB.Driver.Rpc;

namespace SurrealDB.Extensions.Service;

public static class Extensions {
    public static IServiceCollection AddSurrealDB(this IServiceCollection services, Action<ConfigBuilder.Basic> configure) {
        var builder = ConfigBuilder.Create();
        configure(builder);
        Config config = builder.Build();

        services.AddOptions();
        SurrealOptions options = new(){ Configuration = config };
        services.AddOptions<SurrealOptions>()
           .Configure(o => options.Configure(o))
           .Validate(o => options.Validate(null!, o).Succeeded)
           .PostConfigure(o => options.PostConfigure(null!, o));

        if (config.RestEndpoint is not null) {
            DatabaseRest inst = new(in config);
            services.AddSingleton(typeof(IDatabase), inst);
            services.AddSingleton(typeof(DatabaseRest), inst);
        }

        if (config.RpcEndpoint is not null) {
            DatabaseRpc inst = new(in config);
            services.AddSingleton(typeof(IDatabase), inst);
            services.AddSingleton(typeof(DatabaseRpc), inst);
        }

        return services;
    }
}
