# Contribution

Contributions are always welcomed.

In order to contribute, a PR (pull request) must fulfill the requirements below:

- Every Pull Request must have a title AND description.
- Make sure you follow for Writing Commit messages and PR Titles

# Structure

All functional changes are associated with two channels: **stable** and **nightly**.

## Stable

The **stable** channel is associated with the `/master` branch, which is contains very soon to be released changes; it is a release candidate branch.

`/hotfix/**` branches are bug fixes for the release candidate and are merged into master.

## Nightly

The nightly channel is associated with the `/develop` branch, which contains the current - possibly messy - state of development. Some may know this as **trunk**, or **dev**.

**Pull requests originate and are merged into this branch.**

`/feature/**` branches are working branches; this is where the commits go.

## Documentation

Work on the external documentation does not need to strictly conform to the stable, nightly structure.

`/doc/**` branches are associated with the documentation. These branches are merged into `/master`.

## Testing

Tests are always run against the latest version of SurrealDB. Currently SurrealDB has two channels stable and nightly.

The following branches are tested against the **stable** SurrealDB version:

- `/master`: The release candidate.
- `/hotfix/**`: Bugfixes for the release candidate.

The following branches are tested against the **nightly** SurrealDB version:

- `/develop`: The nightly branch.
- `/feature/**`: Working branches.


# Unit tests

If possible a functional change should have its functionality tested, in regards to expected behavior as well as error behavior.

Tests are written using the XUnit framework, and should be added to the test project with the same name.
E.g. a the testcase for change in `src/Driver/Rpc/` lives in `tests/Driver.Tests/`.

### Database tests

If the test requires access to a SurrealDB instance a `DbHandle` is used:

```csharp
public class RestMyDatabaseTests : MyDatabaseTests<DatabaseRest> {
	public RestAuthQueryTests(ITestOutputHelper logger) : base(logger) {
	}
}
public class RpcMyDatabaseTests : MyDatabaseTests<DatabaseRpc> {
	public RpcAuthQueryTests(ITestOutputHelper logger) : base(logger) {
	}
}

public abstract class MyDatabaseTests<T>
	where T : IDatabase, IDisposable, new() {
	protected readonly ITestOutputHelper Logger;

	public MyDatabaseTests(ITestOutputHelper logger) {
		Logger = logger;
	}

	[Fact]
	public async Task TheSpecificFunctionalityThatIsDescribedHereTest() => await DbHandle<T>.WithDatabase(async db => {
		Logger.WriteLine("Here does the test logic for {0}", db);
	});
}

```

Lets break the structure down.

- The generic type in the abstract class `MyDatabaseTests<T>` is the type of the connector being tested.
- The implementations of this class (`RestMyDatabaseTests` and `RpcMyDatabaseTests`) tell XUnit to test the specified connectors.
- `ITestOutputHelper` is used to print information to the untitest protocol.
- Test cases are expession-bodied and call `await DbHandle<T>.WithDatabase(async db => {})`.
- `T db` is a completely frech and empty instance of SurrealDB with the connector `T`.

---

Most importantly: Have fun coding!
