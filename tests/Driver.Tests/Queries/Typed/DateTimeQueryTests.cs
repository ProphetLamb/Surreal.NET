namespace SurrealDB.Driver.Tests.Queries.Typed;
public class RpcDateTimeQueryTests : DateTimeQueryTests<DatabaseRpc> {
    public RpcDateTimeQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestDateTimeQueryTests : DateTimeQueryTests<DatabaseRest> {
    public RestDateTimeQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class DateTimeQueryTests<T> : InequalityQueryTests<T, int, DateTime>
    where T : IDatabase, IDisposable, new() {

    private static IEnumerable<DateTime> TestValues {
        get {
            //yield return new DateTime(2012, 6, 12, 10, 5, 32, 648, DateTimeKind.Utc);
            //yield return new DateTime(2012, 10, 2, 20, 55, 54, 3, DateTimeKind.Utc);
            //yield return new DateTime(2012, 12, 2, 1, 2, 3, 4, DateTimeKind.Utc);
            //yield return DateTime.MaxValue.AsUtc();
            yield return DateTime.MinValue.AsUtc();
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

    protected DateTimeQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
