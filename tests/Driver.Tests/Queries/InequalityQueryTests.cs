namespace SurrealDB.Driver.Tests.Queries;

public abstract class InequalityQueryTests<T, TKey, TValue> : EqualityQueryTests<T, TKey, TValue>
    where T : IDatabase, IDisposable, new() {
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task LessThanQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! < (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 < $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var resultValue = result.GetObject<bool>();
            Assert.Equal(resultValue, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task LessThanOrEqualToQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! <= (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 <= $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var resultValue = result.GetObject<bool>();
            Assert.Equal(resultValue, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task GreaterThanQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! > (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 > $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var resultValue = result.GetObject<bool>();
            Assert.Equal(resultValue, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task GreaterThanOrEqualToQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! >= (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 >= $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var resultValue = result.GetObject<bool>();
            Assert.Equal(resultValue, expectedResult);
        }
    );

    public InequalityQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
