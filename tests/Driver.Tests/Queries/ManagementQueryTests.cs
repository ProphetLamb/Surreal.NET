
// ReSharper disable All

using SurrealDB.Models.Result;

#pragma warning disable CS0169

namespace SurrealDB.Driver.Tests.Queries;
public class RestManagementQueryTests : ManagementQueryTests<DatabaseRest> {
    public RestManagementQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}
public class RpcManagementQueryTests : ManagementQueryTests<DatabaseRpc> {
    public RpcManagementQueryTests(ITestOutputHelper logger) : base(logger) {
    }
}

[Collection("SurrealDBRequired")]
public abstract class ManagementQueryTests<T>
    where T : IDatabase, IDisposable, new() {

    protected readonly ITestOutputHelper Logger;

    public ManagementQueryTests(ITestOutputHelper logger) {
        Logger = logger;
    }

    [Fact]
    public async Task StopStartConnectionTest() => await DbHandle<T>.WithDatabase(
        async db => {
            string sql = "INFO FOR DB;";
            var response = await db.Query(sql, null);
            TestHelper.AssertOk(response);

            await db.Close();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await db.Query(sql, null));
            db.Dispose();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await db.Query(sql, null));

            db = new();
            await db.Open(TestHelper.Default);

            response = await db.Query(sql, null);
            TestHelper.AssertOk(response);
        }
    );

    [Fact]
    public async Task SwitchDatabaseTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var nsName = db.GetConfig().Namespace!;
            var originalDbName = db.GetConfig().Database!;
            var otherDbName = "DifferentDb";

            TestObject<int, string> expectedOriginalObject = new(1, originalDbName);
            TestObject<int, string> expectedOtherObject = new(1, otherDbName);

            Thing thing = new("object", expectedOriginalObject.Key);
            await db.Create(thing, expectedOriginalObject);

            {
                var useResponse = await db.Use(otherDbName, nsName);
                TestHelper.AssertOk(useResponse);

                await db.Create(thing, expectedOtherObject);

                var response = await db.Select(thing);

                TestHelper.AssertOk(response);
                ResultValue result = response.FirstValue();
                TestObject<int, string>? doc = result.AsObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOtherObject);
            }

            {
                var useResponse = await db.Use(originalDbName, nsName);
                TestHelper.AssertOk(useResponse);

                var response = await db.Select(thing);

                TestHelper.AssertOk(response);
                ResultValue result = response.FirstValue();
                TestObject<int, string>? doc = result.AsObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOriginalObject);
            }

        }
    );

    [Fact]
    public async Task SwitchNamespaceTest() => await DbHandle<T>.WithDatabase(
        async db => {
            var originalNsName = db.GetConfig().Namespace!;
            var otherNsName = "DifferentNs";
            var dbName = db.GetConfig().Database!;

            TestObject<int, string> expectedOriginalObject = new(1, originalNsName);
            TestObject<int, string> expectedOtherObject = new(1, otherNsName);

            Thing thing = new("object", expectedOriginalObject.Key);
            await db.Create(thing, expectedOriginalObject);

            {
                var useResponse = await db.Use(dbName, otherNsName);
                TestHelper.AssertOk(useResponse);

                await db.Create(thing, expectedOtherObject);

                var response = await db.Select(thing);

                TestHelper.AssertOk(response);
                ResultValue result = response.FirstValue();
                TestObject<int, string>? doc = result.AsObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOtherObject);
            }

            {
                var useResponse = await db.Use(dbName, originalNsName);
                TestHelper.AssertOk(useResponse);

                var response = await db.Select(thing);

                TestHelper.AssertOk(response);
                ResultValue result = response.FirstValue();
                TestObject<int, string>? doc = result.AsObject<TestObject<int, string>>();
                doc.Should().BeEquivalentTo(expectedOriginalObject);
            }

        }
    );
}
