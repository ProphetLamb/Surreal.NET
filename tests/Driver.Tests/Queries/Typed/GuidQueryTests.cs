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
            yield return Guid.Parse("7909681d-37e5-4373-b868-30fe3430b439");
            yield return Guid.Empty;
        }
    }

    public static IEnumerable<object[]> KeyAndValuePairs {
        get {
            return TestValues.Select(e => new object[] { e, e });
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

    public GuidQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
