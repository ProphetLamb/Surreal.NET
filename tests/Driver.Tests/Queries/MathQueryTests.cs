namespace SurrealDB.Driver.Tests.Queries;

public abstract class MathQueryTests<T, TKey, TValue> : InequalityQueryTests<T, TKey, TValue>
    where T : IDatabase, IDisposable, new() {

    protected abstract string ValueCast();
    protected abstract void AssertEquivalency(TValue a, TValue b);

    [Theory]
    [MemberData("KeyPairs")]
    public async Task AdditionQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! + (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM {ValueCast()}($val1 + $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var resultValue = result.GetObject<TValue>();
            AssertEquivalency(resultValue, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task SubtractionQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! - (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM {ValueCast()}($val1 - $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var value = result.GetObject<TValue>();
            AssertEquivalency(value, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task MultiplicationQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! * (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM {ValueCast()}($val1 * $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var value = result.GetObject<TValue>();
            AssertEquivalency(value, expectedResult);
        }
    );
    
    [Theory]
    [MemberData("KeyPairs")]
    public async Task DivisionQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var divisorIsZero = false;
            dynamic? expectedResult;
            if ((dynamic)val2! != 0) {
                expectedResult = (dynamic)val1! / (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic
            } else {
                divisorIsZero = true;
                expectedResult = default(TValue);
            }

            if (divisorIsZero) {
                // TODO: Remove this when divide by zero works
                // Pass the test right now as Surreal crashes when it tries to divide by 0
                return;
            }

            string sql = $"SELECT * FROM {ValueCast()}($val1 / $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));

            if (!divisorIsZero) {
                var value = result.GetObject<TValue>();
                AssertEquivalency(value, expectedResult);
            } else {
                Assert.True(false); // TODO: Test for the expected result when doing a divide by zero
            }
        }
    );

    protected MathQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
