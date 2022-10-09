using SurrealDB.Models.Result;

namespace SurrealDB.Driver.Tests.Queries;

public abstract class EqualityQueryTests<T, TKey, TValue> : QueryTests<T, TKey, TValue>
    where T : IDatabase, IDisposable, new() {

    [Theory]
    [MemberData("ValuePairs")]
    public async Task EqualsQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! == (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 = $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            var resultValue = result.GetObject<bool>();
            resultValue.Should().Be(expectedResult);
        }
    );

    [Theory]
    [MemberData("ValuePairs")]
    public async Task NotEqualsQueryTest(TValue val1, TValue val2) => await DbHandle<T>.WithDatabase(
        async db => {
            var expectedResult = (dynamic)val1! != (dynamic)val2!; // Can't do operator overloads on generic types, so force it by casting to a dynamic

            string sql = $"SELECT * FROM ($val1 != $val2)";
            Dictionary<string, object?> param = new() { ["val1"] = val1, ["val2"] = val2, };

            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            var resultValue = result.GetObject<bool>();
            resultValue.Should().Be(expectedResult);
        }
    );

    public EqualityQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
