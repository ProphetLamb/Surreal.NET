namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RpcIntQueryTests : IntQueryTests<DatabaseRpc> {
    public RpcIntQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestIntQueryTests : IntQueryTests<DatabaseRest> {
    public RestIntQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class IntQueryTests <T> : MathQueryTests<T, int, int>
    where T : IDatabase, IDisposable, new() {
    
    private static IEnumerable<int> TestValues {
        get {
            yield return 10000; // Can't go too high otherwise the maths operations might overflow
            yield return 0;
            yield return -10000;
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

    protected override string ValueCast() {
        return "<int>";
    }

    private static int RandomInt() {
        return ThreadRng.Shared.Next();
    }

    protected override void AssertEquivalency(int a, int b) {
        b.Should().Be(a);
    }

    protected IntQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
