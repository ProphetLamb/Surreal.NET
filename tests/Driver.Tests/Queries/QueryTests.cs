using SurrealDB.Json;

namespace SurrealDB.Driver.Tests.Queries;

[Collection("SurrealDBRequired")]
public abstract class QueryTests<T, TKey, TValue>
    where T : IDatabase, IDisposable, new() {

    TestDatabaseFixture? fixture;
    protected readonly ITestOutputHelper Logger;

    public QueryTests(ITestOutputHelper logger) {
        Logger = logger;
    }
    
    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);
            string sql = "SELECT * FROM $thing";
            Dictionary<string, object?> param = new() { ["thing"] = thing, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );
    
    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectFromWhereQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);

            string sql = "SELECT * FROM object WHERE id = $thing";
            Dictionary<string, object?> param = new() { ["thing"] = thing };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectFromWhereValueQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);
            Logger.WriteLine("exp: {0}", Serialize(expectedObject));

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);

            string sql = "SELECT * FROM object WHERE Value = $value";
            Dictionary<string, object?> param = new() { ["value"] = expectedObject.Value };

            var response = await db.Query(sql, param);
            Logger.WriteLine("rsp: {0}", response);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            Logger.WriteLine("out: {0}", Serialize(doc));
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [DebuggerStepThrough]
    protected static string Serialize<V>(in V value) {
        return JsonSerializer.Serialize(value, SerializerOptions.Shared);
    }

    [DebuggerStepThrough]
    protected static V? Deserialize<V>(string value) {
        return JsonSerializer.Deserialize<V>(value, SerializerOptions.Shared);

    }
}
