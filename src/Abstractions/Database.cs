using SurrealDB.Configuration;
using SurrealDB.Models;

using DriverResponse = SurrealDB.Models.Result.DriverResponse;

namespace SurrealDB.Abstractions;

/// <summary>
///     Shared interface for interacting with a Surreal database instance
/// </summary>
public interface IDatabase
    : IDisposable {
    /// <summary>
    ///     Returns a copy of the current configuration.
    /// </summary>
    public Config GetConfig();

    /// <summary>
    ///     Opens the connection to a Surreal database instance using the provided configuration.
    ///     Configures the client with all applicable settings.
    /// </summary>
    public Task Open(Config config, CancellationToken ct = default);

    /// <summary>
    ///     Opens the connection to a Surreal database instance using the preconfigured configuration.
    ///     Configures the client with all applicable settings.
    /// </summary>
    public Task Open(CancellationToken ct = default);

    /// <summary>
    ///     Closes the open connection the the Surreal database.
    /// </summary>
    public Task Close(CancellationToken ct = default);

    /// <summary>
    ///     Retrieves the current session information.
    /// </summary>
    public Task<DriverResponse> Info(CancellationToken ct = default);

    /// <summary>
    ///     Switch to a specific namespace and database.
    /// </summary>
    /// <param name="db"> Switches to a specific namespace. </param>
    /// <param name="ns"> Switches to a specific database. </param>
    public Task<DriverResponse> Use(string db, string ns, CancellationToken ct = default);

    /// <summary>
    ///     Signs up to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    public Task<DriverResponse> Signup<TRequest>(TRequest auth, CancellationToken ct = default) where TRequest : IAuth;

    /// <summary>
    ///     Signs in to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<DriverResponse> Signin<TRequest>(TRequest auth, CancellationToken ct = default) where TRequest : IAuth;

    /// <summary>
    ///     Invalidates the authentication for the current connection.
    /// </summary>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<DriverResponse> Invalidate(CancellationToken ct = default);

    /// <summary>
    ///     Authenticates the current connection with a JWT token.
    /// </summary>
    /// <param name="token"> The JWT authentication token. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<DriverResponse> Authenticate(string token, CancellationToken ct = default);

    /// <summary>
    ///     Assigns a value as a parameter for this connection.
    /// </summary>
    /// <param name="key"> Specifies the name of the variable. </param>
    /// <param name="value"> Assigns the value to the variable name. </param>
    public Task<DriverResponse> Let(string key, object? value, CancellationToken ct = default);

    /// <summary>
    ///     Runs a set of SurrealQL statements against the database.
    /// </summary>
    /// #
    /// <param name="sql"> Specifies the SurrealQL statements. </param>
    /// <param name="vars"> Assigns variables which can be used in the query. </param>
    /// <param name="ct"> </param>
    public Task<DriverResponse> Query(string sql, IReadOnlyDictionary<string, object?>? vars = null, CancellationToken ct = default);

    /// <summary>
    ///     Selects all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>SELECT * FROM $thing;</code>
    /// </remarks>
    public Task<DriverResponse> Select(Thing thing, CancellationToken ct = default);

    /// <summary>
    ///     Creates a record in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to create. </param>
    /// <param name="data"> The document / record data to insert. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>CREATE $thing CONTENT $data;</code>
    /// </remarks>
    public Task<DriverResponse> Create(Thing thing, object data, CancellationToken ct = default);

    /// <summary>
    ///     Updates all records in a table, or a specific record, in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to update. </param>
    /// <param name="data"> The document / record data to insert. </param>
    /// <remarks>
    ///     This function replaces the current document / record data with the specified data.
    ///     This function will run the following query in the database:
    ///     <code>UPDATE $thing CONTENT $data;</code>
    /// </remarks>
    public Task<DriverResponse> Update(Thing thing, object data, CancellationToken ct = default);

    /// <summary>
    ///     Modifies all records in a table, or a specific record, in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to update. </param>
    /// <param name="data"> The document / record data to insert. </param>
    /// <remarks>
    ///     This function merges the current document / record data with the specified data.
    ///     This function will run the following query in the database:
    ///     <code>UPDATE $thing MERGE $data;</code>
    /// </remarks>
    public Task<DriverResponse> Change(Thing thing, object data, CancellationToken ct = default);

    /// <summary>
    ///     Applies  <see href="https://jsonpatch.com/"> JSON Patch </see> changes to all records, or a specific record, in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to update. </param>
    /// <param name="data"> The JSON Patch data with which to modify the records. </param>
    /// <remarks>
    ///     This function patches the current document / record data with the specified JSON Patch data.
    ///     This function will run the following query in the database:
    ///     <code>UPDATE $thing PATCH $data;</code>
    /// </remarks>
    public Task<DriverResponse> Modify(Thing thing, Patch[] data, CancellationToken ct = default);

    /// <summary>
    ///     Deletes all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>DELETE * FROM $thing;</code>
    /// </remarks>
    public Task<DriverResponse> Delete(Thing thing, CancellationToken ct = default);
}
