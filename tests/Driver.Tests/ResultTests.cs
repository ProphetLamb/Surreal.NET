using SurrealDB.Models.Result;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Driver.Tests;

public class RestResultTests : ResultTests<DatabaseRest> { }
public class RpcResultTests : ResultTests<DatabaseRpc> { }

[Collection("SurrealDBRequired")]
public abstract class ResultTests<T>
    where T : IDatabase, IDisposable, new() {


    [Theory]
    [InlineData((int)1)]
    [InlineData((int)0)]
    [InlineData((int)-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task IntTryGetValueQueryTest(int expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <int>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out int value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData((long)1)]
    [InlineData((long)0)]
    [InlineData((long)-1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public async Task LongTryGetValueQueryTest(long expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <int>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out long value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData((ulong)1)]
    [InlineData((long)0)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public async Task ULongTryGetValueQueryTest(long expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <int>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out long value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData((float)1.1)]
    [InlineData((float)0)]
    [InlineData((float)-1.1)]
    [InlineData(float.Epsilon)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    // Disable test which can currently not be handled
    // TODO: verify with a newer version of SurrealDB what works, and what need to be adapted.
    // [InlineData(float.PositiveInfinity)]
    // [InlineData(float.NegativeInfinity)]
    // [InlineData(float.NaN)]
    public async Task FloatTryGetValueQueryTest(float expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <float>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            DriverResponse response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out float value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData((double)1.1)]
    [InlineData((double)0)]
    [InlineData((double)-1.1)]
    // Disable test which can currently not be handled
    // TODO: verify with a newer version of SurrealDB what works, and what need to be adapted.
    // [InlineData(double.Epsilon)]
    // [InlineData(double.MaxValue)]
    // [InlineData(double.MinValue)]
    // [InlineData(double.PositiveInfinity)]
    // [InlineData(double.NegativeInfinity)]
    // [InlineData(double.NaN)]
    public async Task DoubleTryGetValueQueryTest(double expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <float>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out double value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BoolTryGetValueQueryTest(bool expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <bool>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeFalse();
            result.IsEmpty.Should().BeFalse();
            result.TryGetValue(out bool value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData(new int[] { 1, 2, 3 })]
    [InlineData(new int[] { 1 })]
    [InlineData(new int[] { })]
    public async Task AsEnumerableQueryTest(int[] expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from $value";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsEmpty.Should().Be(!expectedValue.Any());
            result.IsNullOrUndefined.Should().BeFalse();
            var items = result.AsEnumerable<int>();
            items.Should().BeEquivalentTo(expectedValue);
        }
    );

    [Fact]
    public async Task NullResultQueryTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from NONE";
            var response = await db.Query(sql);

            TestHelper.AssertOk(response);
            ResultValue result = response.FirstValue();
            result.IsNullOrUndefined.Should().BeTrue();
            //result.IsEmpty.Should().BeTrue(); // Not sure if it should be empty or not. This query returns a array of [null]

            result.TryGetValue(out int _).Should().BeFalse();
            result.TryGetValue(out long _).Should().BeFalse();
            result.TryGetValue(out float _).Should().BeFalse();
            result.TryGetValue(out double _).Should().BeFalse();
            result.TryGetValue(out bool _).Should().BeFalse();
            result.AsEnumerable<object>().First().Should().Be(null);
            result.AsObject<object>().Should().BeNull();
        }
    );
}
