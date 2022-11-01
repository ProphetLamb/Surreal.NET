namespace SurrealDB.Driver.Tests.Queries.Typed;

public class RpcEnumFlagQueryTests : EnumFlagQueryTests<DatabaseRpc> {
    public RpcEnumFlagQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RestEnumFlagQueryTests : EnumFlagQueryTests<DatabaseRest> {
    public RestEnumFlagQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

public abstract class EnumFlagQueryTests<T> : EqualityQueryTests<T, FlagsEnum, FlagsEnum>
    where T : IDatabase, IDisposable, new() {


    private static IEnumerable<FlagsEnum> TestValues {
        get {
            var flags = Enum.GetValues(typeof(StandardEnum)).Cast<FlagsEnum>();

            var count = 0;
            foreach (var flags1 in flags) {
                foreach (var flags2 in flags) {
                    count++;
                    if (count >= 4) { // Don't need to do every flag
                        count = 0;
                        yield return flags1 | flags2;
                    }
                }
            }
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

    public EnumFlagQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
