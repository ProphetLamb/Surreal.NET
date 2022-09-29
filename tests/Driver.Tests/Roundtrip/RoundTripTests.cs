using SurrealDB.Common;

namespace SurrealDB.Driver.Tests.Roundtrip;

public class RpcRoundTripTests : RoundTripTests<DatabaseRpc> {
}

public class RestRoundTripTests : RoundTripTests<DatabaseRest> {
}

[Collection("SurrealDBRequired")]
public abstract class RoundTripTests<T>
    where T : IDatabase, IDisposable, new() {


    protected RoundTripObject Expected = new();

    [Fact(Timeout = 10_000)]
    public async Task CreateRoundTripTest() => await DbHandle<T>.WithDatabase(
        async db => {
            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            var response = await db.Create(thing, Expected);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var returnedDocument = result.GetObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(Expected, returnedDocument);
        }
    );

    [Fact(Timeout = 10_000)]
    public async Task CreateAndSelectRoundTripTest() => await DbHandle<T>.WithDatabase(
        async db => {
            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            await db.Create(thing, Expected);
            var response = await db.Select(thing);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var returnedDocument = result.GetObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(Expected, returnedDocument);
        }
    );

    [Fact(Timeout = 10_000)]
    public async Task CreateAndQueryRoundTripTest() => await DbHandle<T>.WithDatabase(
        async db => {
            Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
            await db.Create(thing, Expected);
            string sql = $"SELECT * FROM \"{thing}\"";
            var response = await db.Query(sql, null);

            response.Should().NotBeNull();
            TestHelper.AssertOk(response);
            response.TryGetResult(out Result result).Should().BeTrue();
            var returnedDocument = result.GetObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(Expected, returnedDocument);
        }
    );

    [Fact(Timeout = 10_000)]
    public async Task CreateAndParameterizedQueryRoundTripTest() => await DbHandle<T>.WithDatabase(async db => {
        Thing thing = Thing.From("object", ThreadRng.Shared.Next().ToString());
        var rsp = await db.Create(thing, Expected);
        rsp.IsOk.Should().BeTrue();
        string sql = "SELECT * FROM $thing";
        Dictionary<string, object?> param = new() { ["thing"] = thing, };

        var response = await db.Query(sql, param);

        Assert.NotNull(response);
        TestHelper.AssertOk(response);
        Assert.True(response.TryGetResult(out Result result));
        var returnedDocument = result.GetObject<RoundTripObject>();
        RoundTripObject.AssertAreEqual(Expected, returnedDocument);
    });
}

public class RoundTripObject {

    public string String { get; set; } = "A String";
    public string MultiLineString { get; set; } = "A\nString"; // Fails to write to DB
    public string UnicodeString { get; set; } = "A ❤️";
    public string EmptyString { get; set; } = "";
    public string? NullString { get; set; } = null;

    public int PositiveInteger { get; set; } = int.MaxValue / 2;
    public int NegativeInteger { get; set; } = int.MinValue / 2;
    public int ZeroInteger { get; set; } = 0;
    public int MaxInteger { get; set; } = int.MaxValue;
    public int MinInteger { get; set; } = int.MinValue;
    public int? NullInteger { get; set; } = null;

    public long PositiveLong { get; set; } = long.MaxValue / 2;
    public long NegativeLong { get; set; } = long.MinValue / 2;
    public long ZeroLong { get; set; } = 0;
    public long MaxLong { get; set; } = long.MaxValue;
    public long MinLong { get; set; } = long.MinValue;
    public long? NullLong { get; set; } = null;

    public float PositiveFloat { get; set; } = float.MaxValue / 7;
    public float NegativeFloat { get; set; } = float.MinValue / 7;
    public float ZeroFloat { get; set; } = 0;
    public float MaxFloat { get; set; } = float.MaxValue;
    public float MinFloat { get; set; } = float.MinValue;
    public float NaNFloat { get; set; } = float.NaN; // Not Supported by default by System.Text.Json
    public float EpsilonFloat { get; set; } = float.Epsilon;
    public float NegEpsilonFloat { get; set; } = -float.Epsilon;
    public float PositiveInfinityFloat { get; set; } = float.PositiveInfinity; // Not Supported by default by System.Text.Json
    public float NegativeInfinityFloat { get; set; } = float.NegativeInfinity; // Not Supported by default by System.Text.Json
    public float? NullFloat { get; set; } = null;

    public double PositiveDouble { get; set; } = double.MaxValue / 7;
    public double NegativeDouble { get; set; } = double.MinValue / 7;
    public double ZeroDouble { get; set; } = 0;
    public double MaxDouble { get; set; } = double.MaxValue;
    public double MinDouble { get; set; } = double.MinValue;
    public double NaNDouble { get; set; } = double.NaN; // Not Supported by default by System.Text.Json
    public double EpsilonDouble { get; set; } = double.Epsilon;
    public double NegEpsilonDouble { get; set; } = -double.Epsilon;
    public double PositiveInfinityDouble { get; set; } = double.PositiveInfinity; // Not Supported by default by System.Text.Json
    public double NegativeInfinityDouble { get; set; } = double.NegativeInfinity; // Not Supported by default by System.Text.Json
    public double? NullDouble { get; set; } = null;

    public decimal PositiveDecimal { get; set; } = decimal.MaxValue / 7m;
    public decimal NegativeDecimal { get; set; } = decimal.MinValue / 7m;
    public decimal ZeroDecimal { get; set; } = 0;
    public decimal MaxDecimal { get; set; } = decimal.MaxValue;
    public decimal MinDecimal { get; set; } = decimal.MinValue;
    public decimal? NullDecimal { get; set; } = null;

    public DateTime MaxUtcDateTime { get; set; } = DateTime.MaxValue.AsUtc();
    public DateTime MinUtcDateTime { get; set; } = DateTime.MinValue.AsUtc();
    public DateTime NowUtcDateTime { get; set; } = DateTime.UtcNow;
    public DateTime? NullDateTime { get; set; } = null;

    public DateTimeOffset MaxUtcDateTimeOffset { get; set; } = DateTimeOffset.MaxValue.ToUniversalTime();
    public DateTimeOffset MinUtcDateTimeOffset { get; set; } = DateTimeOffset.MinValue.ToUniversalTime();
    public DateTimeOffset NowUtcDateTimeOffset { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NullDateTimeOffset { get; set; } = null;

    public DateOnly MaxUtcDateOnly { get; set; } = DateOnly.MaxValue;
    public DateOnly MinUtcDateOnly { get; set; } = DateOnly.MinValue;
    public DateOnly NowUtcDateOnly { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? NullUtcDateOnly { get; set; } = null;

    public TimeOnly MaxUtcTimeOnly { get; set; } = TimeOnly.MaxValue;
    public TimeOnly MinUtcTimeOnly { get; set; } = TimeOnly.MinValue;
    public TimeOnly NowUtcTimeOnly { get; set; } = TimeOnly.FromDateTime(DateTime.UtcNow);
    public TimeOnly? NullUtcTimeOnly { get; set; } = null;

    public Guid Guid { get; set; } = Guid.NewGuid();
    public Guid EmptyGuid { get; set; } = Guid.Empty;
    public Guid? NullGuid { get; set; } = null;

    public bool TrueBool { get; set; } = true;
    public bool FalseBool { get; set; } = false;
    public bool? NullBool { get; set; } = null;

    public StandardEnum ZeroStandardEnum { get; set; } = StandardEnum.Zero;
    public StandardEnum OneStandardEnum { get; set; } = StandardEnum.One;
    public StandardEnum TwoHundredStandardEnum { get; set; } = StandardEnum.TwoHundred;
    public StandardEnum NegTwoHundredStandardEnum { get; set; } = StandardEnum.NegTwoHundred;
    public StandardEnum? NullStandardEnum { get; set; } = null;

    public FlagsEnum NoneFlagsEnum { get; set; } = FlagsEnum.None;
    public FlagsEnum AllFlagsEnum { get; set; } = FlagsEnum.All;
    public FlagsEnum SecondFourthFlagsEnum { get; set; } = FlagsEnum.Second | FlagsEnum.Fourth;
    public FlagsEnum UndefinedFlagsEnum { get; set; } = (FlagsEnum)(1 << 8);
    public FlagsEnum? NullFlagsEnum { get; set; } = null;

    public TestObject<int, int>? TestObject { get; set; } = new(-100, 1);
    public TestObject<int, int>? NullTestObject { get; set; } = null;

    public int[] IntArray { get; set; } = new[] { -100, 1, 0, -1, 100 };
    public int[]? NullIntArray { get; set; } = null;

    public TestObject<int, int>?[] TestObjectArray { get; set; } = new[] { new TestObject<int, int>(-100, 1), new TestObject<int, int>(0, -1), null };
    public TestObject<int, int>?[]? NullTestObjectArray { get; set; } = null;

    public static void AssertAreEqual(
        RoundTripObject? a,
        RoundTripObject? b) {
        Assert.NotNull(a);
        Assert.NotNull(b);

        b.Should().BeEquivalentTo(a);
    }
}
