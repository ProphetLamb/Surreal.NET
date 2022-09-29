namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RpcEnumQueryTests : EnumQueryTests<DatabaseRpc> {
    public RpcEnumQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestEnumQueryTests : EnumQueryTests<DatabaseRest> {
    public RestEnumQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class EnumQueryTests<T> : EqualityQueryTests<T, int, StandardEnum>
    where T : IDatabase, IDisposable, new() {

    private static IEnumerable<StandardEnum> TestValues {
        get {
            return Enum.GetValues(typeof(StandardEnum)).Cast<StandardEnum>();
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

    public EnumQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
