namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RpcGuidQueryTests : GuidQueryTests<DatabaseRpc> {
    public RpcGuidQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestGuidQueryTests : GuidQueryTests<DatabaseRest> {
    public RestGuidQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class GuidQueryTests<T> : EqualityQueryTests<T, Guid, Guid>
    where T : IDatabase, IDisposable, new() {


    private static IEnumerable<Guid> TestValues {
        get {
            yield return Guid.NewGuid();
            yield return Guid.Empty;
        }
    }

    public static IEnumerable<object[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object[] { RandomGuid(), e });
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

    private static Guid RandomGuid() {
        return Guid.NewGuid();
    }

    public GuidQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
