namespace SurrealDB.Ws;

internal interface IHandler : IDisposable {

    public string Id { get; }

    public bool Persistent { get; }

    public void Handle(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm);
}

internal sealed class ResponseHandler : IHandler {
    private readonly TaskCompletionSource<(WsManager.RspHeader, WsManager.NtyHeader, Stream)> _tcs = new();
    private readonly string _id;
    private readonly CancellationToken _ct;

    public ResponseHandler(string id, CancellationToken ct) {
        _id = id;
        _ct = ct;
    }

    public Task<(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm)> Task => _tcs!.Task;

    public string Id => _id;

    public bool Persistent => false;

    public void Handle(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm) {
        _tcs.SetResult((rsp, nty, stm));
    }

    public void Dispose() {
        _tcs.TrySetCanceled();
    }

}

internal class NotificationHandler : IHandler, IAsyncEnumerable<(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm)> {
    private readonly Ws _mediator;
    private readonly CancellationToken _ct;
    private TaskCompletionSource<(WsManager.RspHeader, WsManager.NtyHeader, Stream)> _tcs = new();
    public NotificationHandler(Ws mediator, string id, CancellationToken ct) {
        _mediator = mediator;
        Id = id;
        _ct = ct;
    }

    public string Id { get; }
    public bool Persistent => true;

    public void Handle(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm) {
        _tcs.SetResult((rsp, nty, stm));
        _tcs = new();
    }

    public void Dispose() {
        _tcs.TrySetCanceled();
    }

    public async IAsyncEnumerator<(WsManager.RspHeader rsp, WsManager.NtyHeader nty, Stream stm)> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
        while (!_ct.IsCancellationRequested) {
            (WsManager.RspHeader, WsManager.NtyHeader, Stream) res;
            try {
                res = await _tcs.Task;
            } catch (OperationCanceledException) {
                // expected on remove
                yield break;
            }
            yield return res;
        }

        // unregister before throwing
        if (_ct.IsCancellationRequested) {
            _mediator.Unregister(this);
        }
    }
}

