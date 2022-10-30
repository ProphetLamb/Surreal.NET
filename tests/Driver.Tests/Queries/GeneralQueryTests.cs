
// ReSharper disable All

using Superpower.Model;

using SurrealDB.Models.Result;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

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

    private static readonly List<Car> CarRecords = new List<Car> {
        new Car(
            Brand: "Car 1",
            Age: 0,
            RegisteredCountry: "GBR",
            Wheels: new List<Wheel> {
                new Wheel(IsFlat: false, Position: "LF"),
                new Wheel(IsFlat: false, Position: "RF"),
                new Wheel(IsFlat: false, Position: "LR"),
                new Wheel(IsFlat: false, Position: "RR")
            }
        ),
        new Car(
            Brand: "Car 2",
            Age: 2,
            RegisteredCountry: "GBR",
            Wheels: new List<Wheel> {
                new Wheel(IsFlat: true, Position: "LF"),
                new Wheel(IsFlat: false, Position: "RF"),
                new Wheel(IsFlat: false, Position: "LR"),
                new Wheel(IsFlat: false, Position: "RR")
            }
        ),
        new Car(
            Brand: "Car 3",
            Age: 6,
            RegisteredCountry: "USA",
            Wheels: new List<Wheel> {
                new Wheel(IsFlat: false, Position: "CF"),
                /*Three Wheeled Car */
                new Wheel(IsFlat: false, Position: "LR"),
                new Wheel(IsFlat: false, Position: "RR")
            }
        ),
        new Car(
            Brand: "Car 4",
            Age: 8,
            RegisteredCountry: "GBR",
            Wheels: new List<Wheel> {
                new Wheel(IsFlat: false, Position: "LF"),
                new Wheel(IsFlat: false, Position: "RF"),
                new Wheel(IsFlat: false, Position: "LR"),
                new Wheel(IsFlat: false, Position: "RR")
            }
        ),
        new Car(
            Brand: "Car 5",
            Age: 3,
            RegisteredCountry: "USA",
            Wheels: new List<Wheel> {
                new Wheel(IsFlat: false, Position: "LF"),
                new Wheel(IsFlat: false, Position: "RF"),
                new Wheel(IsFlat: true, Position: "LR"),
                new Wheel(IsFlat: false, Position: "RR")
            }
        ),
    };

    private static readonly string CarRecordJson = JsonSerializer.Serialize(CarRecords);

    private record Car(string Brand, int Age, string RegisteredCountry, List<Wheel> Wheels);
    private record Wheel(bool IsFlat, string Position);
    private record FlatWheelResult(List<Wheel> Wheels);
    private record VehicleType(bool IsCar);
    private record OldVehicleResponse(bool IsOldVehicle);
    private record VehicleTypeResult(VehicleType VehicleType);
    private record GroupedCountries(string Country, int Total);

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

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            DateTime? doc = result.AsObject<DateTime>();
            doc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(10));
        }
    );

    [Fact]
    public async Task ExpressionAsAnAliasQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            List<OldVehicleResponse> expectedObject = CarRecords.Select(e => new OldVehicleResponse(e.Age >= 5)).ToList();
            string sql = $@"SELECT Age >= 5 AS IsOldVehicle FROM {CarRecordJson}";

            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            List<OldVehicleResponse>? doc = result.AsObject<List<OldVehicleResponse>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Fact]
    public async Task ManuallyGeneratedObjectStructureQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {

            var vehicleType = new VehicleTypeResult(new VehicleType(true));
            List<VehicleTypeResult> expectedObject = CarRecords.Select(e => vehicleType ).ToList();
            string sql = $@"SELECT {{ IsCar: true }} AS VehicleType FROM {CarRecordJson}";

            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            List<VehicleTypeResult>? doc = result.AsObject<List<VehicleTypeResult>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Fact]
    public async Task FilteredNestedArrayQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            List<FlatWheelResult> expectedObject = CarRecords.Select(e => new FlatWheelResult(e.Wheels.Where(w => w.IsFlat).ToList())).ToList();
            string sql = $@"SELECT Wheels[WHERE IsFlat = true] FROM {CarRecordJson}";

            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            List<FlatWheelResult>? doc = result.AsObject<List<FlatWheelResult>>();
            doc.Should().BeEquivalentTo(expectedObject);
        }
    );

    [Fact]
    public async Task CountAndGroupQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = $@"SELECT
	RegisteredCountry,
	count(Age > 5) AS Total
FROM {CarRecordJson}
GROUP BY RegisteredCountry;";

            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            List<GroupedCountries>? doc = result.AsObject<List<GroupedCountries>>();
            doc.Should().HaveCount(2);
        }
    );

    [Fact]
    public async Task CryptoFunctionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "SELECT * FROM crypto::md5(\"tobie\");";

            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            string? doc = result.AsObject<string>();
            doc.Should().BeEquivalentTo("4768b3fc7ac751e03a614e2349abf3bf");
        }
    );

    [Fact]
    public async Task SimpleAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = 1, f2 = 1, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };
        Thing thing = Thing.From("object", ThreadRng.Shared.Next());
        await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            MathResultDocument? doc = result.AsObject<MathResultDocument>();
            doc.Should().BeEquivalentTo(expectedResult);
        }
    );

    [Fact]
    public async Task EpsilonAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.Epsilon, f2 = float.Epsilon, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            MathResultDocument? doc = result.AsObject<MathResultDocument>();
            doc.Should().NotBeNull();
            doc!.result.Should().BeApproximately(expectedResult.result, 0.000001f);
        }
    );

    [Fact]
    public async Task MinValueAdditionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.MinValue, f2 = float.MaxValue, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 + expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 + f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            MathResultDocument? doc = result.AsObject<MathResultDocument>();
            doc.Should().NotBeNull();
            doc!.result.Should().BeApproximately(expectedResult.result, 0.001f);
        }
    );

    [Fact]
    public async Task MaxValueSubtractionQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            MathRequestDocument expectedObject = new() { f1 = float.MaxValue, f2 = float.MinValue, };
            var expectedResult = new MathResultDocument { result = expectedObject.f1 - expectedObject.f2 };

            Thing thing = Thing.From("object", ThreadRng.Shared.Next());
            await db.Create(thing, expectedObject);

            string sql = "SELECT (f1 - f2) as result FROM $record";
            Dictionary<string, object?> param = new() { ["record"] = thing };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            MathResultDocument? doc = result.AsObject<MathResultDocument>();
            doc.Should().BeEquivalentTo(expectedResult);
        }
    );

    [Fact]
    public async Task MultipleResultSetQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string expectedResult = "A Name";
            string sql = $"LET $name = \"{expectedResult}\";\n"
              + "SELECT * FROM $name;\n";
            var response = await db.Query(sql, null);

            TestHelper.AssertOk(response);
            // we execute two stmts, let and select.
            // let should return null
            response.Oks.First().Value.AsObject<object>().Should().BeNull();
            // select should be array of one string
            var res = response.Oks.Skip(1).First().Value.AsEnumerable<string>();
            res.Count.Should().Be(1);
            res.First().Should().Be(expectedResult);
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
        Thing thing = Thing.From("object", expectedResult.Key);

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

    private static void AssertResponse(DriverResponse response, TestObject<int, int> expectedResult) {
        TestHelper.AssertOk(response);
        Assert.True(response!.TryGetFirstValue(out ResultValue result));
        TestObject<int, int>? doc = result.AsObject<TestObject<int, int>>();
        doc.Should().BeEquivalentTo(expectedResult);
    }
}
