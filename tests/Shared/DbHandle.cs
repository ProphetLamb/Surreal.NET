using SurrealDB.Abstractions;

namespace SurrealDB.Common;

public class DbHandle<T> : IDisposable
    where T: IDatabase, IDisposable, new() {
    private Process? _process;

    private DbHandle(Process p, T db) {
        _process = p;
        Database = db;
    }

    public static async Task<DbHandle<T>> Create() {
        Process p = await Task.Run(SurrealInstance.Create);
        T db = new();
        await db.Open(TestHelper.Default);
        return new(p, db);
    }

    [DebuggerStepThrough]
    public static async Task WithDatabase(Func<T, Task> action) {
        using DbHandle<T> db = await Create();
        await action(db.Database);
    }

    public T Database { get; }

    ~DbHandle() {
        Dispose();
    }

    public void Dispose() {
        Process? p = _process;
        _process = null;
        if (p is not null) {
            Database.Dispose();
            p.Kill();
        }
    }
}
