namespace SurrealDB.Driver.Tests.Queries.Typed;
public class RpcDateTimeOffsetQueryTests : DateTimeOffsetQueryTests<DatabaseRpc> {
    public RpcDateTimeOffsetQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestDateTimeOffsetQueryTests : DateTimeOffsetQueryTests<DatabaseRest> {
    public RestDateTimeOffsetQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class DateTimeOffsetQueryTests<T> : InequalityQueryTests<T, int, DateTimeOffset>
    where T : IDatabase, IDisposable, new() {

    private static IEnumerable<DateTimeOffset> TestValues {
        get {
            yield return new DateTimeOffset(2012, 6, 12, 10, 5, 32, 648, TimeSpan.Zero);
            //yield return DateTimeOffset.MaxValue.ToUniversalTime();
            yield return DateTimeOffset.MinValue.ToUniversalTime();
        }
    }

    public static IEnumerable<object[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object[] { RandomInt(), e });
        }
    }
    
    public static IEnumerable<object[]> ValuePairs {
        get {
            foreach (var testValue1 in TestValues) {
                foreach (var testValue2 in TestValues) {
                    yield return new object[] { testValue1, testValue2 };
                }
            }
        }
    }

    private static int RandomInt() {
        return ThreadRng.Shared.Next();
    }

    private static DateTimeOffset RandomDateTimeOffset() {
        var minDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var maxDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var diff = (maxDate - minDate).TotalMicroseconds();
        var randomDateTime = minDate.AddMicroseconds((long)(ThreadRng.Shared.NextDouble() * diff));
        return randomDateTime;
    }

    public DateTimeOffsetQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
