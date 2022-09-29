using System.Diagnostics;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace SurrealDB.Models;

/// <summary>
/// JSON Patch is a format for describing changes to a JSON document. It can be used to avoid sending a whole document when only a part has changed. When used in combination with the HTTP PATCH method, it allows partial updates for HTTP APIs in a standards compliant way.
/// </summary>
public readonly record struct Patch {
    [DebuggerStepThrough, JsonConstructor]
    private Patch(Mode op, string path, string? from, object? value) {
        this.op = op;
        this.path = path;
        this.from = from;
        this.value = value;
    }

    /// <inheritdoc cref="Mode.add"/>
    [DebuggerStepThrough]
    public static Patch Add(string path, object value) => new(Mode.add, path, default, value);
    /// <inheritdoc cref="Mode.remove"/>
    [DebuggerStepThrough]
    public static Patch Remove(string path) => new(Mode.remove, path, default, default);
    /// <inheritdoc cref="Mode.replace"/>
    [DebuggerStepThrough]
    public static Patch Replace(string path, object value) => new(Mode.replace, path, default, value);
    /// <inheritdoc cref="Mode.copy"/>
    [DebuggerStepThrough]
    public static Patch Copy(string path, string from) => new(Mode.copy, path, from, default);
    /// <inheritdoc cref="Mode.move"/>
    [DebuggerStepThrough]
    public static Patch Move(string path, string from) => new(Mode.move, path, from, default);
    /// <inheritdoc cref="Mode.test"/>
    [DebuggerStepThrough]
    public static Patch Test(string path, object value) => new(Mode.test, path, default, value);

    /// <summary>The operation to perform.</summary>
    public Mode op { get; }
    /// <summary>The absolute path of the change.</summary>
    public string path { get; }
    /// <summary>The new value of the element at the path. Only for operations "copy", "move".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? from { get; }
    /// <summary>The new value of the element at the path. Only for operations "add", "replace", "test".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object? value { get; }

    /// <summary>
    /// A JSON Patch document is just a JSON file containing an array of patch operations. The patch operations supported by JSON Patch are “add”, “remove”, “replace”, “move”, “copy” and “test”. The operations are applied in order: if any of them fail then the whole patch operation should abort.
    /// </summary>
    public enum Mode : byte {
        /// <summary>Adds a value to an object or inserts it into an array. In the case of an array, the value is inserted before the given index. The - character can be used instead of an index to insert at the end of an array.</summary>
        /// <example>`{ "op": "add", "path": "/biscuits/1", "value": { "name": "Ginger Nut" } }`</example>
        add,
        /// <summary>Removes a value from an object or array. --OR-- Removes the first element of the array at biscuits (or just removes the “0” key if biscuits is an object)</summary>
        /// <example>`{ "op": "remove", "path": "/biscuits" }` --OR-- `{ "op": "remove", "path": "/biscuits/0" }`</example>
        remove,
        /// <summary>Replaces a value. Equivalent to a “remove” followed by an “add”.</summary>
        /// <example>`{ "op": "replace", "path": "/biscuits/0/name", "value": "Chocolate Digestive" }`</example>
        replace,
        /// <summary>Moves a value from one location to the other. Both from and path are JSON Pointers.</summary>
        /// <example>`{ "op": "move", "from": "/biscuits", "path": "/cookies" }`</example>
        copy,
        /// <summary>Copies a value from one location to another within the JSON document. Both from and path are JSON Pointers.</summary>
        /// <example>`{ "op": "copy", "from": "/biscuits/0", "path": "/best_biscuit" }`</example>
        move,
        /// <summary>Tests that the specified value is set in the document. If the test fails, then the patch as a whole should not apply.</summary>
        /// <example>`{ "op": "test", "path": "/best_biscuit/name", "value": "Choco Leibniz" }`</example>
        test,
    }
}
