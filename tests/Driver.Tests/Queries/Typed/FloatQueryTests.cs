namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RpcFloatQueryTests : FloatQueryTests<DatabaseRpc> {
    public RpcFloatQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestFloatQueryTests : FloatQueryTests<DatabaseRest> {
    public RestFloatQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class FloatQueryTests <T> : MathQueryTests<T, float, float>
    where T : IDatabase, IDisposable, new() {

    private static IEnumerable<float> TestValues {
        get {
            yield return 1000; // Can't go too high otherwise the maths operations might overflow
            yield return 0;
            yield return -1000;
        }
    }

    public static IEnumerable<object[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object[] { RandomFloat(), e });
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

    protected override string ValueCast() {
        return "<float>";
    }

    private static float RandomFloat() {
        return ThreadRng.Shared.NextSingle();
    }

    protected override void AssertEquivalency(float a, float b) {
        b.Should().BeApproximately(a, 0.1f);
    }

    protected FloatQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
