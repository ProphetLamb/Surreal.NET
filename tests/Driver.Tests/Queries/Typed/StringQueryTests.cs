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
            yield return "Test";
            yield return "Test123";
            yield return "Test 123";
            yield return "Test-123";
            yield return "Test_123";
            yield return "Test\n123";
            yield return "";
            yield return null;
        }
    }

    public static IEnumerable<object?[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object?[] { RandomString(), e });
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

    private static string RandomString(int length = 10) {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
           .Select(s => s[ThreadRng.Shared.Next(s.Length)]).ToArray());
    }

    public StringQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
