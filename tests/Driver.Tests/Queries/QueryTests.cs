using SurrealDB.Json;
using SurrealDB.Models.Result;

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

            Thing thing = new("object", expectedObject.Key!);
            var response = await db.Create(thing, expectedObject);

            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);
            var response = await db.Select(thing);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleDeleteTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);
            var deleteResponse = await db.Delete(thing);

            TestHelper.AssertOk(deleteResponse);

            string sql = "SELECT * FROM $thing";
            Dictionary<string, object?> param = new() { ["thing"] = thing, };
            var selectResponse = await db.Query(sql, param);
            TestHelper.AssertOk(selectResponse);
            selectResponse.TryGetFirstValue(out ResultValue result).Should().BeTrue();
            result.Inner.ValueKind.Should().Be(JsonValueKind.Array);
            result.Inner.GetArrayLength().Should().Be(0);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleUpdateTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, default(TValue)!);

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);
            expectedObject.Value = value;
            var response = await db.Update(thing, expectedObject);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleModifyTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> createdObject = new(key, default(TValue)!);
            ExtendedTestObject<TKey, TValue> expectedObject = new(key, value, value);

            Thing thing = new("object", createdObject.Key!);
            await db.Create(thing, createdObject);
            await db.Modify(thing, new[]{
                Patch.Replace("/Value", value!),
                Patch.Add("/MergeValue", value!)
            });

            // Modify return the applied JSON patch from the request!
            // Select the altered object, and validate against the expected object.
            var response = await db.Select(thing);
            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<ExtendedTestObject<TKey, TValue>>();
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

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TValue? doc = result.AsObject<TValue>();
            doc.Should().BeEquivalentTo(val1);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleChangeTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> createdObject = new(key, default(TValue)!);
            ExtendedTestObject<TKey, TValue> expectedObject = new(key, value, value);

            Thing thing = new("object", createdObject.Key!);
            await db.Create(thing, createdObject);
            var response = await db.Change(thing, new {  Value = value, MergeValue = value });

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<ExtendedTestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);
            string sql = "SELECT * FROM $thing";
            Dictionary<string, object?> param = new() { ["thing"] = thing, };

            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectFromWhereQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);

            string sql = "SELECT * FROM object WHERE id = $thing";
            Dictionary<string, object?> param = new() { ["thing"] = thing };

            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleSelectFromWhereValueQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);
            Logger.WriteLine("exp: {0}", Serialize(expectedObject));

            Thing thing = new("object", expectedObject.Key!);
            await db.Create(thing, expectedObject);

            string sql = "SELECT * FROM object WHERE Value = $value";
            Dictionary<string, object?> param = new() { ["value"] = expectedObject.Value };

            var response = await db.Query(sql, param);
            Logger.WriteLine("rsp: {0}", response);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            TestObject<TKey, TValue>? doc = result.AsObject<TestObject<TKey, TValue>>();
            Logger.WriteLine("out: {0}", Serialize(doc));
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Theory]
    [MemberData("KeyAndValuePairs")]
    public async Task SimpleRelateAndGraphSemanticsQueryTest(TKey key, TValue value) => await DbHandle<T>.WithDatabase(
        async db => {
            TestObject<TKey, TValue> expectedObject = new(key, value);

            Thing thing1 = new("object1", expectedObject.Key!);
            await db.Create(thing1, expectedObject);

            Thing thing2 = new("object2", expectedObject.Key!);
            await db.Create(thing2, expectedObject);

            var relateSql = "RELATE ($thing1)->hasOtherThing->($thing2)";
            Dictionary<string, object?> vars = new() {
                {"thing1", thing1},
                {"thing2", thing2},
            };
            var relateResponse = await db.Query(relateSql, vars);
            TestHelper.AssertOk(relateResponse);

            var thing2Sql = "SELECT ->hasOtherThing->object2.* AS field FROM $thing1";
            var thing2Response = await db.Query(thing2Sql, vars);
            TestHelper.AssertOk(thing2Response);
            ResultValue thing2Result = thing2Response.FirstValue();
            Field<TestObject<TKey, TValue>>? thing2Doc = thing2Result.AsObject<Field<TestObject<TKey, TValue>>>();
            thing2Doc.Should().NotBeNull();
            thing2Doc!.field.First().Should().BeEquivalentTo(expectedObject);
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
