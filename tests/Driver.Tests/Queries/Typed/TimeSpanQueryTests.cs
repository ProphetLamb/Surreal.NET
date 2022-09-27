namespace SurrealDB.Driver.Tests.Queries.Typed;
public class RpcTimeSpanQueryTests : TimeSpanQueryTests<DatabaseRpc> {
    public RpcTimeSpanQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestTimeSpanQueryTests : TimeSpanQueryTests<DatabaseRest> {
    public RestTimeSpanQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class TimeSpanQueryTests<T> : InequalityQueryTests<T, int, TimeSpan>
    where T : IDatabase, IDisposable, new() {
    
    private static IEnumerable<TimeSpan> TestValues {
        get {
            yield return new TimeSpan(1, 2, 3, 4, 5);
            yield return new TimeSpan(200, 20, 34, 41, 65);
            yield return TimeSpan.Zero;
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
    
    public TimeSpanQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
