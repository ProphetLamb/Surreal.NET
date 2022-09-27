using SurrealDB.Configuration;
using SurrealDB.Models;

namespace SurrealDB.Abstractions;

/// <summary>
///     Shared interface for interacting with a Surreal database instance
/// </summary>
public interface IDatabase<TResponse>
    : IDatabase
    where TResponse : IResponse {

    /// <summary>
    ///     Retrieves the current session information.
    /// </summary>
    /// <param name="ct"> </param>
    public new Task<TResponse> Info(CancellationToken ct = default);

    /// <summary>
    ///     Switch to a specific namespace and database.
    /// </summary>
    /// <param name="db"> Switches to a specific namespace. </param>
    /// <param name="ns"> Switches to a specific database. </param>
    public new Task<TResponse> Use(string db, string ns, CancellationToken ct = default);

    /// <summary>
    ///     Signs up to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    public new Task<TResponse> Signup(object auth, CancellationToken ct = default);

    /// <summary>
    ///     Signs in to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public new Task<TResponse> Signin(object auth, CancellationToken ct = default);

    /// <summary>
    ///     Invalidates the authentication for the current connection.
    /// </summary>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public new Task<TResponse> Invalidate(CancellationToken ct = default);

    /// <summary>
    ///     Authenticates the current connection with a JWT token.
    /// </summary>
    /// <param name="token"> The JWT authentication token. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public new Task<TResponse> Authenticate(string token, CancellationToken ct = default);

    /// <summary>
    ///     Assigns a value as a parameter for this connection.
    /// </summary>
    /// <param name="key"> Specifies the name of the variable. </param>
    /// <param name="value"> Assigns the value to the variable name. </param>
    public new Task<TResponse> Let(string key, object? value, CancellationToken ct = default);

    /// <summary>
    ///     Runs a set of SurrealQL statements against the database.
    /// </summary>
    /// #
    /// <param name="sql"> Specifies the SurrealQL statements. </param>
    /// <param name="vars"> Assigns variables which can be used in the query. </param>
    public new Task<TResponse> Query(string sql, IReadOnlyDictionary<string, object?>? vars, CancellationToken ct = default);

    /// <summary>
    ///     Selects all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>SELECT * FROM $thing;</code>
    /// </remarks>
    public new Task<TResponse> Select(Thing thing, CancellationToken ct = default);

    /// <summary>
    ///     Creates a record in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to create. </param>
    /// <param name="data"> The document / record data to insert. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>CREATE $thing CONTENT $data;</code>
    /// </remarks>
    public new Task<TResponse> Create(Thing thing, object data, CancellationToken ct = default);

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
    public new Task<TResponse> Update(Thing thing, object data, CancellationToken ct = default);

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
    public new Task<TResponse> Change(Thing thing, object data, CancellationToken ct = default);

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
    public new Task<TResponse> Modify(Thing thing, object data, CancellationToken ct = default);

    /// <summary>
    ///     Deletes all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>DELETE * FROM $thing;</code>
    /// </remarks>
    public new Task<TResponse> Delete(Thing thing, CancellationToken ct = default);
}

/// <summary>
///     Shared interface for interacting with a Surreal database instance
/// </summary>
public interface IDatabase {
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
    public Task<IResponse> Info(CancellationToken ct = default);

    /// <summary>
    ///     Switch to a specific namespace and database.
    /// </summary>
    /// <param name="db"> Switches to a specific namespace. </param>
    /// <param name="ns"> Switches to a specific database. </param>
    public Task<IResponse> Use(string db, string ns, CancellationToken ct = default);

    /// <summary>
    ///     Signs up to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    public Task<IResponse> Signup(object auth, CancellationToken ct = default);

    /// <summary>
    ///     Signs in to a specific authentication scope.
    /// </summary>
    /// <param name="auth"> Variables used in a signin query. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<IResponse> Signin(object auth, CancellationToken ct = default);

    /// <summary>
    ///     Invalidates the authentication for the current connection.
    /// </summary>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<IResponse> Invalidate(CancellationToken ct = default);

    /// <summary>
    ///     Authenticates the current connection with a JWT token.
    /// </summary>
    /// <param name="token"> The JWT authentication token. </param>
    /// <remarks>
    ///     This updates the internal <see cref="Config" />.
    /// </remarks>
    public Task<IResponse> Authenticate(string token, CancellationToken ct = default);

    /// <summary>
    ///     Assigns a value as a parameter for this connection.
    /// </summary>
    /// <param name="key"> Specifies the name of the variable. </param>
    /// <param name="value"> Assigns the value to the variable name. </param>
    public Task<IResponse> Let(string key, object? value, CancellationToken ct = default);

    /// <summary>
    ///     Runs a set of SurrealQL statements against the database.
    /// </summary>
    /// #
    /// <param name="sql"> Specifies the SurrealQL statements. </param>
    /// <param name="vars"> Assigns variables which can be used in the query. </param>
    /// <param name="ct"> </param>
    public Task<IResponse> Query(string sql, IReadOnlyDictionary<string, object?>? vars, CancellationToken ct = default);

    /// <summary>
    ///     Selects all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>SELECT * FROM $thing;</code>
    /// </remarks>
    public Task<IResponse> Select(Thing thing, CancellationToken ct = default);

    /// <summary>
    ///     Creates a record in the database.
    /// </summary>
    /// <param name="thing"> The table name or the specific record id to create. </param>
    /// <param name="data"> The document / record data to insert. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>CREATE $thing CONTENT $data;</code>
    /// </remarks>
    public Task<IResponse> Create(Thing thing, object data, CancellationToken ct = default);

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
    public Task<IResponse> Update(Thing thing, object data, CancellationToken ct = default);

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
    public Task<IResponse> Change(Thing thing, object data, CancellationToken ct = default);

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
    public Task<IResponse> Modify(Thing thing, object data, CancellationToken ct = default);

    /// <summary>
    ///     Deletes all records in a table, or a specific record, from the database.
    /// </summary>
    /// <param name="thing"> The table name or a record id to select. </param>
    /// <remarks>
    ///     This function will run the following query in the database:
    ///     <code>DELETE * FROM $thing;</code>
    /// </remarks>
    public Task<IResponse> Delete(Thing thing, CancellationToken ct = default);
}
