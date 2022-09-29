using SurrealDB.Json;

namespace SurrealDB.Driver.Tests.Queries;

[Collection("SurrealDBRequired")]
public abstract class QueryTests<T, TKey, TValue>
    where T : IDatabase, IDisposable, new() {

    protected readonly ITestOutputHelper Logger;

    public QueryTests(ITestOutputHelper logger) {
        Logger = logger;
    }

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleCreateTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            var response = await db.Create(thing, expectedObject);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);
            var response = await db.Select(thing);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleDeleteTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);
            var deleteResponse = await db.Delete(thing);

            Assert.NotNull(deleteResponse);
            TestHelper.AssertOk(deleteResponse);

            var selectResponse = await db.Select(thing);
            Assert.NotNull(selectResponse);
            TestHelper.AssertOk(selectResponse);
            Assert.True(selectResponse.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            Assert.Null(doc);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleUpdateTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, default(TValue)!);

            Thing thing = Thing.From("object", expectedObject.Key!.ToString());
            await db.Create(thing, expectedObject);
            expectedObject.Value = value;
            var response = await db.Update(thing, expectedObject);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleModifyTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> createdObject = new(key, default(TValue)!);
            ExtendedTestObject<TKey, TValue> expectedObject = new(key, value, value);

            Thing thing = Thing.From("object", createdObject.Key!.ToString());
            await db.Create(thing, createdObject);
            await db.Modify(thing, new[] {
                new {  op = "replace", path = "/Value", value = value },
                new {  op = "add", path = "/MergeValue", value = value },
            });

            // Modify return the applied JSON patch from the request!
            // Select the altered object, and validate against the expected object.
            var response = await db.Select(thing);
            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<ExtendedTestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("ValuePairs")]
    public async Task SimpleLetTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            await db.Let("key", val1);

            string sql = "SELECT * FROM $key";
            var response = await db.Query(sql);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TValue? doc = result.GetObject<TValue>();
            doc.Should().BeEquivalentTo(val1);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleChangeTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> createdObject = new(key, default(TValue)!);
            ExtendedTestObject<TKey, TValue> expectedObject = new(key, value, value);

            Thing thing = Thing.From("object", createdObject.Key!.ToString());
            await db.Create(thing, createdObject);
            var response = await db.Change(thing, new {  Value = value, MergeValue = value });

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            TestObject<TKey, TValue>? doc = result.GetObject<ExtendedTestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

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
