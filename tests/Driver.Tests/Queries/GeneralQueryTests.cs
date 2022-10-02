using SurrealDB.Common;
// ReSharper disable All
#pragma warning disable CS0169

namespace SurrealDB.Driver.Tests.Queries;
public class RestGeneralQueryTests : GeneralQueryTests<DatabaseRest> {
    public RestGeneralQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RpcGeneralQueryTests : GeneralQueryTests<DatabaseRpc> {
    public RpcGeneralQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

[Collection("SurrealDBRequired")]
public abstract class GeneralQueryTests<T>
    where T : IDatabase, IDisposable, new() {

    protected readonly ITestOutputHelper Logger;

    public GeneralQueryTests(ITestOutputHelper logger) {
        Logger = logger;
    }


    private record GroupedCountries {
        string? country;
        string? total;
    }

    private class MathRequestDocument {
        public float f1 {get; set;}
        public float f2 {get; set;}
    }

    private class MathResultDocument {
        public float result {get; set;}
    }

    [Fact]
    public async Task StopStartConnectionTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "INFO FOR DB;";
            var response = await db.Query(sql, null);
            Assert.NotNull(response);
            TestHelper.AssertOk(response);

            await db.Close();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await db.Query(sql, null));
            db.Dispose();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await db.Query(sql, null));

            db = new();
            await db.Open(TestHelper.Default);
            
            response = await db.Query(sql, null);
            Assert.NotNull(response);
            TestHelper.AssertOk(response);
        }
    );

    [Fact]
    public async Task SwitchDatabaseTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var nsName = db.GetConfig().Namespace!;
            var originalDbName = db.GetConfig().Database!;
            var otherDbName = "DifferentDb";

            TestObject<int, string> expectedOriginalObject = new(1, originalDbName);
            TestObject<int, string> expectedOtherObject = new(1, otherDbName);
            
            Thing thing = Thing.From("object", expectedOriginalObject.Key.ToString());
            await db.Create(thing, expectedOriginalObject);

            {
                var useResponse = await db.Use(otherDbName, nsName);
                Assert.NotNull(useResponse);
                TestHelper.AssertOk(useResponse);

                await db.Create(thing, expectedOtherObject);

                var response = await db.Select(thing);

                Assert.NotNull(response);
                TestHelper.AssertOk(response);
                Assert.True(response.TryGetResult(out Result result));
                TestObject<int, string>? doc = result.GetObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOtherObject);
            }

            {
                var useResponse = await db.Use(originalDbName, nsName);
                Assert.NotNull(useResponse);
                TestHelper.AssertOk(useResponse);
                
                var response = await db.Select(thing);

                Assert.NotNull(response);
                TestHelper.AssertOk(response);
                Assert.True(response.TryGetResult(out Result result));
                TestObject<int, string>? doc = result.GetObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOriginalObject);
            }

        }
    );

    [Fact]
    public async Task SwitchNamespaceTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var originalNsName = db.GetConfig().Namespace!;
            var otherNsName = "DifferentNs";
            var dbName = db.GetConfig().Database!;

            TestObject<int, string> expectedOriginalObject = new(1, originalNsName);
            TestObject<int, string> expectedOtherObject = new(1, otherNsName);
            
            Thing thing = Thing.From("object", expectedOriginalObject.Key.ToString());
            await db.Create(thing, expectedOriginalObject);

            {
                var useResponse = await db.Use(dbName, otherNsName);
                Assert.NotNull(useResponse);
                TestHelper.AssertOk(useResponse);

                await db.Create(thing, expectedOtherObject);

                var response = await db.Select(thing);

                Assert.NotNull(response);
                TestHelper.AssertOk(response);
                Assert.True(response.TryGetResult(out Result result));
                TestObject<int, string>? doc = result.GetObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOtherObject);
            }

            {
                var useResponse = await db.Use(dbName, originalNsName);
                Assert.NotNull(useResponse);
                TestHelper.AssertOk(useResponse);

                var response = await db.Select(thing);

                Assert.NotNull(response);
                TestHelper.AssertOk(response);
                Assert.True(response.TryGetResult(out Result result));
                TestObject<int, string>? doc = result.GetObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOriginalObject);
            }

        }
    );

    [Fact]
    public async Task SimpleFuturesQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <future> { time::now() };";

            var response = await db.Query(sql, null);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            DateTime? doc = result.GetObject<DateTime>();
            doc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(10));
        }
    );

    [Fact]
    public async Task CountAndGroupQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = @"SELECT
	country,
	count(age > 30) AS total
FROM [
	{ age: 33, country: 'GBR' },
	{ age: 45, country: 'GBR' },
	{ age: 39, country: 'USA' },
	{ age: 29, country: 'GBR' },
	{ age: 43, country: 'USA' }
]
GROUP BY country;";

            var response = await db.Query(sql, null);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            List<GroupedCountries>? doc = result.GetObject<List<GroupedCountries>>();
            doc.Should().HaveCount(2);
        }
    );

    [Fact]
    public async Task CryptoFunctionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "SELECT * FROM crypto::md5(\"tobie\");";

            var response = await db.Query(sql, null);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            string? doc = result.GetObject<string>();
            doc.Should().BeEquivalentTo("4768b3fc7ac751e03a614e2349abf3bf");
        }
    );

    [Fact]
    public async Task SimpleAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = 1, f2 = 1, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };
        Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
        await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            MathResultDocument? doc = result.GetObject<MathResultDocument>();
            doc.Should().BeEquivalentTo(expectedResult);
        }
    );

    [Fact]
    public async Task EpsilonAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.Epsilon, f2 = float.Epsilon, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            MathResultDocument? doc = result.GetObject<MathResultDocument>();
            Assert.NotNull(doc);
            doc!.result.Should().BeApproximately(expectedResult.result, 0.000001f);
        }
    );

    [Fact]
    public async Task MinValueAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.MinValue, f2 = float.MaxValue, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            MathResultDocument? doc = result.GetObject<MathResultDocument>();
            Assert.NotNull(doc);
            doc!.result.Should().BeApproximately(expectedResult.result, 0.001f);
        }
    );

    [Fact]
    public async Task MaxValueSubtractionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.MaxValue, f2 = float.MinValue, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 - expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 - f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            MathResultDocument? doc = result.GetObject<MathResultDocument>();
            doc.Should().BeEquivalentTo(expectedResult);
        }
    );

    [Fact]
    public async Task SimultaneousDatabaseOperations() => await DbHandle<T>.WithDatabase(
        async db => {
            var taskCount = 50;
            var tasks = Enumerable.Range(0, taskCount).Select(i => DbTask(i, db));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    );

    private async Task DbTask(int i, T db) {
        Logger.WriteLine($"Start {i} - Thread ID {Thread.CurrentThread.ManagedThreadId}");

        var expectedResult = new TestObject<int, int>(i, i);
        Thing thing = Thing.From("object", expectedResult.Key.ToString());

        var createResponse = await db.Create(thing, expectedResult).ConfigureAwait(false);
        AssertResponse(createResponse, expectedResult);
        Logger.WriteLine($"Create {i} - Thread ID {Thread.CurrentThread.ManagedThreadId}");
        
        var selectResponse = await db.Select(thing).ConfigureAwait(false);
        AssertResponse(selectResponse, expectedResult);
        Logger.WriteLine($"Select {i} - Thread ID {Thread.CurrentThread.ManagedThreadId}");
        
        string sql = "SELECT * FROM $record";
        Dictionary<string, object?> param = new() { ["record"] = thing };
        var queryResponse = await db.Query(sql, param).ConfigureAwait(false);
        AssertResponse(queryResponse, expectedResult);
        Logger.WriteLine($"Query {i} - Thread ID {Thread.CurrentThread.ManagedThreadId}");

        Logger.WriteLine($"End {i} - Thread ID {Thread.CurrentThread.ManagedThreadId}");
    }

    private static void AssertResponse(IResponse? response, TestObject<int, int> expectedResult) {
        Assert.NotNull(response);
        TestHelper.AssertOk(response);
        Assert.True(response.TryGetResult(out Result result));
        TestObject<int, int>? doc = result.GetObject<TestObject<int, int>>();
        doc.Should().BeEquivalentTo(expectedResult);
    }
}
