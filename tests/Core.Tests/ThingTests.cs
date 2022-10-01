namespace SurrealDB.Core.Tests;

public class ThingTests {
    private static readonly List<string> Keys = new() { "{0}ThingKey", "Thing{0}Key", "ThingKey{0}", /*"{0}"*/ };
    private static readonly List<char> ComplexChars = new() { '-', ' ', '❤'};
    private static readonly List<char> StandardChars = new() { '_', '0'};

    public static List<object[]> StandardKeys => Keys.SelectMany(s => StandardChars.Select(c => string.Format(s, c))).Append("ThingKey").Select(e => new object[]{e}).ToList();
    public static List<object[]> ComplexKeys => Keys.SelectMany(s => ComplexChars.Select(c => string.Format(s, c))).Select(e => new object[]{e}).ToList();
    
    [Fact]
    public void TableSurrealThing() {
        var table = "TableName";

        var surrealThing = Thing.From(table);
        
        surrealThing.ToString().Should().BeEquivalentTo(table);
        surrealThing.Table.ToString().Should().BeEquivalentTo(table);
        surrealThing.TableAndSeparator.ToString().Should().BeEquivalentTo(table);
        surrealThing.Key.ToString().Should().BeEmpty();
        surrealThing.ToUri().Should().BeEquivalentTo(table);
    }
    
    [Theory]
    [MemberData(nameof(StandardKeys))]
    public void TableAndKeySurrealThing(string key) {
        var table = "TableName";
        var expectedThing = $"{table}:{key}";

        var surrealThing = Thing.From(table, key);
        
        surrealThing.ToString().Should().BeEquivalentTo(expectedThing);
        surrealThing.Table.ToString().Should().BeEquivalentTo(table);
        surrealThing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        surrealThing.Key.ToString().Should().BeEquivalentTo(key);
        surrealThing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }
    
    [Theory]
    [MemberData(nameof(StandardKeys))]
    public void TableAndKeyFromStringSurrealThing(string key) {
        var table = "TableName";
        var expectedThing = $"{table}:{key}";

        var surrealThing = Thing.From(expectedThing);

        surrealThing.ToString().Should().BeEquivalentTo(expectedThing);
        surrealThing.Table.ToString().Should().BeEquivalentTo(table);
        surrealThing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        surrealThing.Key.ToString().Should().BeEquivalentTo(key);
        surrealThing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }
    
    [Theory]
    [MemberData(nameof(ComplexKeys))]
    public void TableAndKeyWithComplexCharacterSurrealThing(string key) {
        var table = "TableName";
        var escapedKey = $"{Thing.CHAR_PRE}{key}{Thing.CHAR_SUF}";
        var expectedThing = $"{table}:{escapedKey}";

        var surrealThing = Thing.From(table, key).Escape();

        surrealThing.ToString().Should().BeEquivalentTo(expectedThing);
        surrealThing.Table.ToString().Should().BeEquivalentTo(table);
        surrealThing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        surrealThing.Unescape().Key.ToString().Should().BeEquivalentTo(key);
        surrealThing.Key.ToString().Should().BeEquivalentTo(escapedKey);
        surrealThing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }
    
    [Theory]
    [MemberData(nameof(ComplexKeys))]
    public void TableAndKeyAlreadyEscapedSurrealThing(string key) {
        var table = "TableName";
        var escapedKey = $"{Thing.CHAR_PRE}{key}{Thing.CHAR_SUF}";
        var expectedThing = $"{table}:{escapedKey}";

        var surrealThing = Thing.From(table, escapedKey).Escape();
        
        surrealThing.ToString().Should().BeEquivalentTo(expectedThing);
        surrealThing.Table.ToString().Should().BeEquivalentTo(table);
        surrealThing.TableAndSeparator.ToString().Should().BeEquivalentTo($"{table}:");
        surrealThing.Unescape().Key.ToString().Should().BeEquivalentTo(key);
        surrealThing.Key.ToString().Should().BeEquivalentTo(escapedKey);
        surrealThing.ToUri().Should().BeEquivalentTo($"{table}/{Uri.EscapeDataString(key)}");
    }
}
