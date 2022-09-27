namespace SurrealDB.Driver.Tests.Queries.Typed;
public class RpcDateOnlyQueryTests : DateOnlyQueryTests<DatabaseRpc> {
    public RpcDateOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestDateOnlyQueryTests : DateOnlyQueryTests<DatabaseRest> {
    public RestDateOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class DateOnlyQueryTests<T> : InequalityQueryTests<T, int, DateOnly>
    where T : IDatabase, IDisposable, new() {
    
    private static IEnumerable<DateOnly> TestValues {
        get {
            yield return new DateOnly(2012, 6, 12);
            yield return new DateOnly(2012, 10, 2);
            yield return DateOnly.MaxValue;
            yield return DateOnly.MinValue;
        }
    }

    public static IEnumerable<object[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object[] { RandomInt(), e });
        }
    }
    
    public static IEnumerable<object[]> KeyPairs {
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

    private static DateOnly RandomDateOnly() {
        var minDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var maxDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var diff = (maxDate - minDate).TotalMicroseconds();
        var randomeDateTime = minDate.AddMicroseconds((long)(ThreadRng.Shared.NextDouble() * diff));
        return DateOnly.FromDateTime(randomeDateTime);
    }

    protected DateOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
