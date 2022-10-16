namespace SurrealDB.Driver.Tests;

public sealed class RpcDatabaseTest : DatabaseTestDriver<DatabaseRpc> {

}

public sealed class RestDatabaseTest : DatabaseTestDriver<DatabaseRest> {

}

[Collection("SurrealDBRequired")]
public abstract class DatabaseTestDriver<T>
    : DriverBase<T>
    where T : IDatabase, IDisposable, new() {

    protected override async Task Run(T db) {
        db.GetConfig().Should().BeEquivalentTo(TestHelper.Default);

        var useResp = await db.Use(TestHelper.Database, TestHelper.Namespace);
        TestHelper.AssertOk(useResp);
        var infoResp = await db.Info();
        TestHelper.AssertOk(infoResp);

        var signInStatus = await db.Signin(new RootAuth(TestHelper.User, TestHelper.Pass));

        TestHelper.AssertOk(signInStatus);
        //AssertOk(await db.Invalidate());

        (string id1, string id2) = ("id1", "id2");
        var res1 = await db.Create(
            "person",
            new {
                Title = "Founder & CEO",
                Name = new { First = "Tobie", Last = "Morgan Hitchcock", },
                Marketing = true,
                Identifier = ThreadRng.Shared.Next(),
            }
        );

        TestHelper.AssertOk(res1);

        var res2 = await db.Create(
            "person",
            new {
                Title = "Contributor",
                Name = new { First = "Prophet", Last = "Lamb", },
                Marketing = false,
                Identifier = ThreadRng.Shared.Next(),
            }
        );

        TestHelper.AssertOk(res2);

        Thing thing2 = new("person", id2);
        TestHelper.AssertOk(await db.Update(thing2, new { Marketing = false, }));

        TestHelper.AssertOk(await db.Select(thing2));

        TestHelper.AssertOk(await db.Delete(thing2));

        Thing thing1 = new("person", id1);
        TestHelper.AssertOk(
            await db.Change(
                thing1,
                new {
                    Title = "Founder & CEO",
                    Name = new { First = "Tobie", Last = "Hitchcock Morgan", },
                    Marketing = false,
                    Identifier = ThreadRng.Shared.Next(),
                }
            )
        );

        string newTitle = "Founder & CEO & Ruler of the known free World";
        var modifyResp = await db.Modify(thing1, new[] {
            Patch.Replace("/Title", newTitle),
        });
        TestHelper.AssertOk(modifyResp);

        TestHelper.AssertOk(await db.Let("tbl", "person"));

        var queryResp = await db.Query(
            "SELECT $props FROM $tbl WHERE title = $title",
            new Dictionary<string, object?> { ["props"] = "title, identifier", ["tbl"] = "person", ["title"] = newTitle, }
        );

        TestHelper.AssertOk(queryResp);

        await db.Close();
    }
}

/// <summary>
///     The test driver executes the testsuite on the client.
/// </summary>
[Collection("SurrealDBRequired")]
public abstract class DriverBase<T>
    where T : IDatabase, IDisposable, new() {


    [Fact]
    public async Task TestSuite() {
        using var handle = await DbHandle<T>.Create();
        await Run(handle.Database);
    }

    protected abstract Task Run(T db);
}
