using SurrealDB.Configuration;
using SurrealDB.Driver.Rpc;

using DatabaseRpc db = new();

// Signin as a namespace, database, or root user
Config cfg = Config.Create()
   .WithEndpoint("127.0.0.1:8000")
   .WithDatabase("test")
   .WithNamespace("test")
   .WithBasicAuth("root", "root")
   .WithRpc()
   .Build();

await db.Open(cfg);

// Create a new person with a random id
var created = await db.Create("person", new {
    title = "Founder & CEO",
    name = new {
        first = "Tobie",
        last = "Morgan Hitchcock"
    },
    marketing = true,
    identifier = Random.Shared.Next().ToString("x")
});

// Update a person record with a specific id
var updated = await db.Change("person:jamie", new {
    marketing = true,
});

// Select all people records
var poeple = await db.Select("person");

// Perform a custom advanced query
var groups = await db.Query(
    "SELECT marketing, count() FROM type::table($tb) GROUP BY markering",
    new Dictionary<string, object?> {
        ["tb"] = "person"
    }
);

