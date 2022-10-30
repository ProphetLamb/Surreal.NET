using SurrealDB.Shared.Tests;

using Xunit.Abstractions;

namespace SurrealDB.Core.Tests;

public class ThingTests {

    protected readonly ITestOutputHelper Logger;

    public ThingTests(ITestOutputHelper logger) {
        Logger = logger;
    }

    private static readonly List<string> Keys = new() { "{0}ThingKey", "Thing{0}Key", "ThingKey{0}", "{0}" };
    // Includes chars that are:
    // - UTF8 Multi Byte
    // - could be considered alphanumeric but are not standard latin characters.
    // - could be considered a underscore, but isn't the right one.
    // - Are before and after the alphanumeric latin characters and underscore.
    // - Are a Combining Character (such as zalgo text).
    private static readonly List<string> ComplexChars = new() { "/", ":", "@", "[", "`", "{", "-", " ", "❤", "\n", "\"", "$", "£", "ह", "€", "한", "𐍈", "Γ", "²", "¼", "௯", "௰", "༳", "➈", "‗", "⎁", "⎂", "a̱", "a̲", "a̲̲", "ʰ", "◌̲", "◌͝", "◌⃠", "a̷̢̘̩̣̩̹̝͔̹̝̎̄̒͊̄̈̋͐͋̀̕͠͝" };
    private static readonly List<string> StandardChars = new() { "_", "0", "9", "a", "z", "A", "Z"};

    private static readonly List<object> Numbers = new() {
        byte.MinValue, (byte) 11, (byte) 111, byte.MaxValue,
        sbyte.MinValue, (sbyte) -12, (sbyte) 0, (sbyte) 12, sbyte.MaxValue,
        short.MinValue, (short) -123, (short) 0, (short) 123, short.MaxValue,
        ushort.MinValue, (ushort) 0, (ushort) 13, (ushort) 123, ushort.MaxValue,
        int.MinValue, (int) -123, (int) 0, (int) 123, int.MaxValue,
        uint.MinValue, (uint) 13, (uint) 123, uint.MaxValue,
        long.MinValue, (long) -123, (long) 0, (long) 123, long.MaxValue,
        ulong.MinValue, (ulong) 13, (ulong) 123, ulong.MaxValue,
        float.MinValue, (float) -123.456, (float) -13, (long) 0, (float) 13, (float) 123.456, float.MaxValue,
        double.MinValue, (double) -123.456, (double) -13, (double) 0, (double) 13, (double) 123.456, double.MaxValue,
        decimal.MinValue, (decimal) -123.4567, (decimal) -13, (decimal) 0, (decimal) 13, (decimal) 123.4567, decimal.MaxValue,
    };

    private static readonly List<(object key, string expectedKey, bool shouldBeEscaped)> Objects = new() {
        (new {TestString = "test"}, "{\"TestString\":\"test\"}", false),
        (new {Test_String = "test"}, "{\"Test_String\":\"test\"}", false),
        (new {Test0String = "test"}, "{\"Test0String\":\"test\"}", false),
        (new {TestString = "test", TestNumber = 123}, "{\"TestString\":\"test\",\"TestNumber\":123}", false),
        (new List<string>{"string 1", "string 2"}, "[\"string 1\",\"string 2\"]", false),
        (new List<int>{1, 2, 3}, "[1,2,3]", false),
        (new List<object>{123, new DateTime(2012, 6, 12, 10, 5, 32, 648, DateTimeKind.Utc)}, "[123,\"2012-06-12T10:05:32.6480000Z\"]", false),
        (true, "true", false), (false, "false", false),
        (new Guid("80ff994d-355d-4afd-9704-bad3f53e020b"), "80ff994d-355d-4afd-9704-bad3f53e020b", true),
        (new DateTime(2012, 6, 12, 10, 5, 32, 648, DateTimeKind.Utc), "2012-06-12T10:05:32.6480000Z", true),
        (new DateTimeOffset(2012, 6, 12, 10, 5, 32, 648, TimeSpan.Zero), "2012-06-12T10:05:32.6480000+00:00", true),
        (new DateOnly(2012, 10, 2), "2012-10-02", true),
        (new TimeOnly(10, 5, 32, 648), "10:05:32.6480000", true),
        (new TimeSpan(200, 20, 34, 41, 265), "200d20h34m41s265ms", false),
        (FlagsEnum.None, "0", false),
        (FlagsEnum.Third, "4", false),
        (FlagsEnum.Third | FlagsEnum.First, "5", false),
        (FlagsEnum.All, "15", false),
        (StandardEnum.Zero, "0", false),
        (StandardEnum.TwoHundred, "200", false),
        (StandardEnum.NegTwoHundred, "-200", false),
    };

    public static List<object[]> StandardStringKeys => Keys.SelectMany(s => StandardChars.Select(c => string.Format(s, c))).Append("ThingKey").Append("").Select(e => new object[]{e}).ToList();
    public static List<object[]> ComplexStringKeys => Keys.SelectMany(s => ComplexChars.Select(c => string.Format(s, c))).Select(e => new object[]{e}).ToList();
    public static List<object[]> NumberKeys => Numbers.Select(e => new []{e, e.GetType()}).ToList();
    public static List<object[]> ObjectKeys => Objects.Select(e => new []{e.key, e.expectedKey, e.shouldBeEscaped, e.key.GetType()}).ToList();

    [Fact]
    public void TableStringNoKeyThing() {
        var table = "TableName";

        Thing thing = table;
        Logger.WriteLine("Thing: {0}", thing);

        thing.ToString().Should().BeEquivalentTo(table);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo(table);
        thing.Key.ToString().Should().BeEmpty();
        thing.IsKeyEscaped.Should().BeFalse();
        thing.HasKey.Should().BeFalse();
        thing.ToUri().Should().BeEquivalentTo(table);
    }

    [Theory]
    [MemberData(nameof(StandardStringKeys))]
    public void TableAndStringKeyThing(string key) {
        var table = "TableName";
        var expectedThing = $"{table}:{key}";

        Thing thing = (table, key);
        Logger.WriteLine("Thing: {0}", thing);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(key);
        thing.IsKeyEscaped.Should().BeFalse();
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }

    [Theory]
    [MemberData(nameof(StandardStringKeys))]
    public void TableAndStringKeyFromStringThing(string key) {
        var table = "TableName";
        var expectedThing = $"{table}:{key}";

        Thing thing = expectedThing;
        Logger.WriteLine("Thing: {0}", thing);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(key);
        thing.IsKeyEscaped.Should().BeFalse();
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }

    [Theory]
    [MemberData(nameof(ComplexStringKeys))]
    public void TableAndStringKeyWithComplexCharacterThing(string key) {
        var table = "TableName";
        var escapedKey = $"{ThingHelper.PREFIX}{key}{ThingHelper.SUFFIX}";
        var expectedThing = $"{table}:{escapedKey}";

        var thing = Thing.From(table, key);
        Logger.WriteLine("Thing: {0}", thing);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(escapedKey);
        thing.IsKeyEscaped.Should().BeTrue();
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }

    [Theory]
    [MemberData(nameof(ComplexStringKeys))]
    public void TableAndStringKeyAlreadyEscapedThing(string key) {
        var table = "TableName";
        var escapedKey = $"{ThingHelper.PREFIX}{key}{ThingHelper.SUFFIX}";
        var expectedThing = $"{table}:{escapedKey}";

        var thing = Thing.From(table, escapedKey);
        Logger.WriteLine("Thing: {0}", thing);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(escapedKey);
        thing.IsKeyEscaped.Should().BeTrue();
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }

    [Theory]
    [MemberData(nameof(NumberKeys))]
    public void TableAndNumberKeyThing(object key, Type type) {
        var table = "TableName";
        var expectedThing = $"{table}:{key}";

        var thing = Thing.From(table, key);
        Logger.WriteLine("Thing: {0} ({1})", thing, type);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(key.ToString());
        thing.IsKeyEscaped.Should().BeFalse();
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key.ToString()!)}");
    }

    [Theory]
    [MemberData(nameof(ObjectKeys))]
    public void TableAndObjectKeyThing(object key, string unescapedKey, bool shouldBeEscaped, Type type) {
        var table = "TableName";
        string expectedKey = shouldBeEscaped ? $"{ThingHelper.PREFIX}{unescapedKey}{ThingHelper.SUFFIX}" : unescapedKey;
        string expectedThing = $"{table}{ThingHelper.SEPARATOR}{expectedKey}";

        var thing = (table, key).ToThing();
        Logger.WriteLine("Thing: {0} ({1})", thing, type);

        thing.ToString().Should().BeEquivalentTo(expectedThing);
        thing.Table.ToString().Should().BeEquivalentTo(table);
        thing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        thing.Key.ToString().Should().BeEquivalentTo(expectedKey);
        thing.IsKeyEscaped.Should().Be(shouldBeEscaped);
        thing.HasKey.Should().BeTrue();
        thing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(unescapedKey)}");
    }

    [Fact]
    public void NullThingPropertyAccessors() {
        Thing t = default;
        t.HasKey.Should().BeFalse();
        t.Table.ToString().Should().BeEmpty();
        t.TableAndSeparator.ToString().Should().BeEmpty();
        t.Key.ToString().Should().BeEmpty();
    }

    [Fact]
    public void NullThingSerialized() {
        Thing t = default;
        var json = JsonSerializer.Serialize(t, SerializerOptions.Shared);
        json.Should().Be("\"\"");
        Thing empty = "";
        var emptyJson = JsonSerializer.Serialize(empty, SerializerOptions.Shared);
        emptyJson.Should().BeEquivalentTo(json);
    }
}
