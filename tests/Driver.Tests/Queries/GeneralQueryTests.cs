using SurrealDB.Common;
// ReSharper disable All
#pragma warning disable CS0169

namespace SurrealDB.Driver.Tests.Queries;
public class RestGeneralQueryTests : GeneralQueryTests<DatabaseRest> { }
public class RpcGeneralQueryTests : GeneralQueryTests<DatabaseRpc> { }

[Collection("SurrealDBRequired")]
public abstract class GeneralQueryTests<T>
    where T : IDatabase, IDisposable, new() {


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

    [Fact(Skip = "Blocked by https://github.com/ProphetLamb/Surreal.Net/issues/20")]
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
}
