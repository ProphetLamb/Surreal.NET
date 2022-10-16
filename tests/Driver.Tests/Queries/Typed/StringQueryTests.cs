namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RestStringQueryTests : StringQueryTests<DatabaseRest> {
    public RestStringQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RpcStringQueryTests : StringQueryTests<DatabaseRpc> {
    public RpcStringQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class StringQueryTests<T> : EqualityQueryTests<T, string, string>
    where T : IDatabase, IDisposable, new() {
    
    private static IEnumerable<string?> TestValues {
        get {
            yield return "TestValue";
            yield return "Test123Value";
            // Re-enable when https://github.com/surrealdb/surrealdb/issues/1364 is fixed
            //yield return "Test Value";
            //yield return "Test-Value";
            //yield return "Test_Value";
            //yield return "Test\nValue";
            //yield return "Test❤Value";
            //yield return "Test$Value";
            //yield return "Test£Value";
            //yield return "TestहValue";
            //yield return "Test€Value";
            //yield return "Test𐍈Value";
            //yield return "";
        }
    }

    public static IEnumerable<object?[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object?[] { e, e });
        }
    }

    public static IEnumerable<object?[]> ValuePairs {
        get {
            foreach (var testValue1 in TestValues) {
                foreach (var testValue2 in TestValues) {
                    yield return new object?[] { testValue1, testValue2 };
                }
            }
        }
    }

    public StringQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
