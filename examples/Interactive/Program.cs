
using System.ComponentModel.DataAnnotations;
using System.Net;

using Polly;

using Sharprompt;

using SurrealDB.Abstractions;
using SurrealDB.Configuration;
using SurrealDB.Driver.Rest;
using SurrealDB.Driver.Rpc;

Console.CancelKeyPress += (_, _) => Environment.Exit(0);

IDatabase db = await ReadOpen();
while (true) {
    await ReadQuery(db);
}

static IDatabase ReadDatabase() {
    var (prot, cfg) = ReadConfig();
    return prot switch {
        Prot.Rest => new DatabaseRest(cfg),
        _ => new DatabaseRpc(cfg),
    };
}

static (Prot, Config) ReadConfig() {
    Policy policy = Policy.Handle<Exception>().RetryForever();
    var c = Config.Create()
        .WithEndpoint(policy.ExecuteAndCapture(() => IPEndPoint.Parse(Prompt.Input<string>("Endpoint"))).Result)
        .WithNamespace(Prompt.Input<string>("Namespace"))
        .WithDatabase(Prompt.Input<string>("Database"))
        .WithBasicAuth(Prompt.Input<string>("Username"), Prompt.Input<string>("Password"));
    return Prompt.Select<Prot>("Protocol") switch {
        Prot.Rest => (Prot.Rest, c.WithRest(true).Build()),
        _ => (Prot.Rpc, c.WithRpc(true).Build()),
    };
}

static async Task<IDatabase> ReadOpen() {
    IDatabase db = ReadDatabase();
    await db.Open();
    Console.WriteLine("Connected");
    return db;
}

async Task ReadQuery(IDatabase db) {
    AsyncPolicy policy = Policy.Handle<Exception>().RetryForeverAsync();
    await policy.ExecuteAsync(async () => {
        var query = Prompt.Input<string>("Query");
        var result = await db.Query(query, null);
        Console.WriteLine(result);
    });
}

enum Prot {
    [Display(Name = "RPC")]
    Rpc = 1,
    [Display(Name = "REST")]
    Rest = 2,
}
