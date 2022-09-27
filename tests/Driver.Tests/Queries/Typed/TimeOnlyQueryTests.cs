namespace SurrealDB.Driver.Tests.Queries.Typed;
public class RpcTimeOnlyQueryTests : TimeOnlyQueryTests<DatabaseRpc> {
    public RpcTimeOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestTimeOnlyQueryTests : TimeOnlyQueryTests<DatabaseRest> {
    public RestTimeOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class TimeOnlyQueryTests<T> : InequalityQueryTests<T, int, TimeOnly>
    where T : IDatabase, IDisposable, new(){

    private static IEnumerable<TimeOnly> TestValues {
        get {
            yield return new TimeOnly(10, 5, 32, 648);
            yield return new TimeOnly(20, 55, 54, 3);
            yield return new TimeOnly(1, 2, 3, 4);
            yield return TimeOnly.MaxValue;
            yield return TimeOnly.MinValue;
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

    public TimeOnlyQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
