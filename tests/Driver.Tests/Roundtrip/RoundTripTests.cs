using System.Collections;

using SurrealDB.Json;
using SurrealDB.Models.Result;

namespace SurrealDB.Driver.Tests.Roundtrip;

public class RpcRoundTripTests : RoundTripTests<DatabaseRpc> {
    public RpcRoundTripTests(ITestOutputHelper logger) : base(logger) {
    }
}

public class RestRoundTripTests : RoundTripTests<DatabaseRest> {
    public RestRoundTripTests(ITestOutputHelper logger) : base(logger) {
    }
}

[Collection("SurrealDBRequired")]
public abstract class RoundTripTests<T>
    where T : IDatabase, IDisposable, new() {

    protected readonly ITestOutputHelper Logger;

    public RoundTripTests(ITestOutputHelper logger) {
        Logger = logger;
    }


    private static IEnumerable<RoundTripObject> DocumentsToTest {
        get {
            //yield return new RoundTripObject();

            for (int i = 4; i < 9; i++) {
                var arraySize = 2 << i;
                var largeDocument = new RoundTripObject {
                    StringArray = new string[arraySize],
                };

                for (int n = 0; n < largeDocument.StringArray.Length; n++) {
                    largeDocument.StringArray[n] = TestHelper.RandomString(10);
                }

                yield return largeDocument;
            }
        }
    }

    public static List<object[]> Documents => DocumentsToTest.Select(e => new []{(object)e}).ToList();

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CreateRoundTripTest(RoundTripObject document) => await DbHandle<T>.WithDatabase(
        async db => {
            Logger.WriteLine("Document size in bytes: {0}", JsonSerializer.Serialize(document, SerializerOptions.Shared).Length * sizeof(char));

            Thing thing = ("object", ThreadRng.Shared.Next()).ToThing();
            var response = await db.Create(thing, document);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            var returnedDocument = result.AsObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(document, returnedDocument);
        }
    );

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CreateAndSelectRoundTripTest(RoundTripObject document) => await DbHandle<T>.WithDatabase(
        async db => {
            Logger.WriteLine("Document size in bytes: {0}", JsonSerializer.Serialize(document, SerializerOptions.Shared).Length * sizeof(char));

            Thing thing = ("object", ThreadRng.Shared.Next()).ToThing();
            await db.Create(thing, document);
            var response = await db.Select(thing);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            var returnedDocument = result.AsObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(document, returnedDocument);
        }
    );

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CreateAndQueryRoundTripTest(RoundTripObject document) => await DbHandle<T>.WithDatabase(
        async db => {
            Logger.WriteLine("Document size in bytes: {0}", JsonSerializer.Serialize(document, SerializerOptions.Shared).Length * sizeof(char));

            Thing thing = ("object", ThreadRng.Shared.Next()).ToThing();
            await db.Create(thing, document);
            string sql = $"SELECT * FROM \"{thing}\"";
            var response = await db.Query(sql, null);

            response.Should().NotBeNull();
            TestHelper.AssertOk(response);
            response.TryGetFirstValue(out ResultValue result).Should().BeTrue();
            var returnedDocument = result.AsObject<RoundTripObject>();
            RoundTripObject.AssertAreEqual(document, returnedDocument);
        }
    );

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CreateAndParameterizedQueryRoundTripTest(RoundTripObject document) => await DbHandle<T>.WithDatabase(async db => {
        Logger.WriteLine("Document size in bytes: {0}", JsonSerializer.Serialize(document, SerializerOptions.Shared).Length * sizeof(char));

        Thing thing = ("object", ThreadRng.Shared.Next()).ToThing();
        var rsp = await db.Create(thing, document);
        rsp.HasErrors.Should().BeFalse();
        string sql = "SELECT * FROM $thing";
        Dictionary<string, object?> param = new() { ["thing"] = thing, };

        var response = await db.Query(sql, param);

        TestHelper.AssertOk(response);
        ResultValue result = response.FirstValue();
        var returnedDocument = result.AsObject<RoundTripObject>();
        RoundTripObject.AssertAreEqual(document, returnedDocument);
    });

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task CreateManyAndQueryRoundTripTest(RoundTripObject document) => await DbHandle<T>.WithDatabase(
        async db => {
            var count = 10;
            Logger.WriteLine("Document size in bytes: {0} x {1}", JsonSerializer.Serialize(document, SerializerOptions.Shared).Length * sizeof(char), count);

            for (int i = 0; i < count; i++) {
                Thing thing = ("object", ThreadRng.Shared.Next()).ToThing();
                await db.Create(thing, document);
            }

            string sql = "SELECT * FROM object";
            var response = await db.Query(sql, null);

            response.Should().NotBeNull();
            TestHelper.AssertOk(response);
            response.TryGetFirstValue(out ResultValue result).Should().BeTrue();
            var returnedDocuments = result.AsEnumerable<RoundTripObject>().ToList();
            returnedDocuments.Count.Should().Be(count);
            foreach (var returnedDocument in returnedDocuments) {
                RoundTripObject.AssertAreEqual(document, returnedDocument);
            }
        }
    );
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

    private static readonly DateTime s_dateTime = new (2012, 6, 12, 10, 5, 32, 648, DateTimeKind.Utc);
    //public DateTime MaxUtcDateTime { get; set; } = DateTime.MaxValue.AsUtc();
    public DateTime MinUtcDateTime { get; set; } = DateTime.MinValue.AsUtc();
    public DateTime UtcDateTime { get; set; } = s_dateTime;
    public DateTime? NullDateTime { get; set; } = null;

    //public DateTimeOffset MaxUtcDateTimeOffset { get; set; } = DateTimeOffset.MaxValue.ToUniversalTime();
    public DateTimeOffset MinUtcDateTimeOffset { get; set; } = DateTimeOffset.MinValue.ToUniversalTime();
    public DateTimeOffset UtcDateTimeOffset { get; set; } = new (s_dateTime);
    public DateTimeOffset? NullDateTimeOffset { get; set; } = null;

    //public DateOnly MaxUtcDateOnly { get; set; } = DateOnly.MaxValue;
    public DateOnly MinUtcDateOnly { get; set; } = DateOnly.MinValue;
    public DateOnly UtcDateOnly { get; set; } = DateOnly.FromDateTime(s_dateTime);
    public DateOnly? NullUtcDateOnly { get; set; } = null;

    //public TimeOnly MaxUtcTimeOnly { get; set; } = TimeOnly.MaxValue;
    public TimeOnly MinUtcTimeOnly { get; set; } = TimeOnly.MinValue;
    public TimeOnly UtcTimeOnly { get; set; } = TimeOnly.FromDateTime(s_dateTime);
    public TimeOnly? NullUtcTimeOnly { get; set; } = null;

    //public TimeSpan MaxTimeSpan { get; set; } = TimeSpan.MaxValue;
    //public TimeSpan MinTimeSpan { get; set; } = TimeSpan.MinValue;
    public TimeSpan TimeSpan { get; set; } = new (200, 20, 34, 41, 265);
    public TimeSpan? NullTimeSpan { get; set; } = null;

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

    public int[] IntArray { get; set; } = {-100, 1, 0, -1, 100};
    public int[]? NullIntArray { get; set; } = null;
    public string[] StringArray { get; set; } = {"/", ":", "@", "[", "`", "{", "-", " ", "❤", "\n", "\"", "$", "£", "ह", "€", "한",/* "𐍈",*/ "Γ", "²", "¼", "௯", "௰", "༳", "➈", "‗", "⎁", "⎂", "a̱", "a̲", "a̲̲", "ʰ", "◌̲", "◌͝", "◌⃠", "a̷̢̘̩̣̩̹̝͔̹̝̎̄̒͊̄̈̋͐͋̀̕͠͝", "_", "0", "9", "a", "z", "A", "Z"};

    public TestObject<int, int>?[] TestObjectArray { get; set; } = new [] { new TestObject<int, int>(-100, 1), new TestObject<int, int>(0, -1), null };
    public TestObject<int, int>?[]? NullTestObjectArray { get; set; } = null;

    public static void AssertAreEqual(
        RoundTripObject? a,
        RoundTripObject? b) {
        a.Should().NotBeNull();
        b.Should().NotBeNull();

        b.Should().BeEquivalentTo(a);
    }
}
