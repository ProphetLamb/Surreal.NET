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

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var wasSuccessful = result.TryGetValue(out int value);
            wasSuccessful.Should().BeTrue();
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

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var wasSuccessful = result.TryGetValue(out long value);
            wasSuccessful.Should().BeTrue();
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
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    public async Task FloatTryGetValueQueryTest(float expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <float>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var wasSuccessful = result.TryGetValue(out float value);
            wasSuccessful.Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );

    [Theory]
    [InlineData((double)1.1)]
    [InlineData((double)0)]
    [InlineData((double)-1.1)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public async Task DoubleTryGetValueQueryTest(double expectedValue) => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "select * from <float>($value)";
            Dictionary<string, object?> param = new() { ["value"] = expectedValue };
            var response = await db.Query(sql, param);

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var wasSuccessful = result.TryGetValue(out double value);
            wasSuccessful.Should().BeTrue();
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

            Assert.NotNull(response);
            TestHelper.AssertOk(response);
            Assert.True(response.TryGetResult(out Result result));
            var wasSuccessful = result.TryGetValue(out bool value);
            wasSuccessful.Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    );
}
