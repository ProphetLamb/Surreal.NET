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

        IResponse useResp = await db.Use(TestHelper.Database, TestHelper.Namespace);
        AssertOk(useResp);
        IResponse infoResp = await db.Info();
        AssertOk(infoResp);

        IResponse signInStatus = await db.Signin(new{ user = TestHelper.User, pass = TestHelper.Pass, });

        AssertOk(signInStatus);
        //AssertOk(await db.Invalidate());

        (string id1, string id2) = ("id1", "id2");
        IResponse res1 = await db.Create(
            "person",
            new {
                Title = "Founder & CEO",
                Name = new { First = "Tobie", Last = "Morgan Hitchcock", },
                Marketing = true,
                Identifier = ThreadRng.Shared.Next(),
            }
        );

        AssertOk(res1);

        IResponse res2 = await db.Create(
            "person",
            new {
                Title = "Contributor",
                Name = new { First = "Prophet", Last = "Lamb", },
                Marketing = false,
                Identifier = ThreadRng.Shared.Next(),
            }
        );

        AssertOk(res2);

        Thing thing2 = Thing.From("person", id2);
        AssertOk(await db.Update(thing2, new { Marketing = false, }));

        AssertOk(await db.Select(thing2));

        AssertOk(await db.Delete(thing2));

        Thing thing1 = Thing.From("person", id1);
        AssertOk(
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
        IResponse modifyResp = await db.Modify(thing1, new[] {
            Patch.Replace("/Title", newTitle),
        });
        AssertOk(modifyResp);

        AssertOk(await db.Let("tbl", "person"));

        IResponse queryResp = await db.Query(
            "SELECT $props FROM $tbl WHERE title = $title",
            new Dictionary<string, object?> { ["props"] = "title, identifier", ["tbl"] = "person", ["title"] = newTitle, }
        );

        AssertOk(queryResp);

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

    [DebuggerStepThrough]
    protected void AssertOk(
        in IResponse rpcResponse,
        [CallerArgumentExpression("rpcResponse")]
        string caller = "") {
        if (!rpcResponse.TryGetError(out Error err)) {
            return;
        }

        Exception ex = new($"Expected Ok, got {err.Code} ({err.Message}) in {caller}");
        throw ex;
    }
}
