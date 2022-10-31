namespace SurrealDB.Ws;

internal interface IHandler : IDisposable {

    public string Id { get; }

    public bool Persistent { get; }

    public void Dispatch(WsHeaderWithMessage header);
}

internal sealed class ResponseHandler : IHandler {
    private readonly TaskCompletionSource<WsHeaderWithMessage> _tcs = new();
    private readonly string _id;
    private readonly CancellationToken _ct;

    public ResponseHandler(string id, CancellationToken ct) {
        _id = id;
        _ct = ct;
    }

    public Task<WsHeaderWithMessage> Task => _tcs!.Task;

    public string Id => _id;

    public bool Persistent => false;

    public void Dispatch(WsHeaderWithMessage header) {
        _ct.ThrowIfCancellationRequested();
        _tcs.SetResult(header);
    }

    public void Dispose() {
        _tcs.TrySetCanceled();
    }

}

internal class NotificationHandler : IHandler, IAsyncEnumerable<WsHeaderWithMessage> {
    private readonly WsTxMessageMediator _mediator;
    private readonly CancellationToken _ct;
    private TaskCompletionSource<WsHeaderWithMessage> _tcs = new();
    public NotificationHandler(WsTxMessageMediator mediator, string id, CancellationToken ct) {
        _mediator = mediator;
        Id = id;
        _ct = ct;
    }

    public string Id { get; }
    public bool Persistent => true;

    public void Dispatch(WsHeaderWithMessage header) {
        _ct.ThrowIfCancellationRequested();
        _tcs.SetResult(header);
        _tcs = new();
    }

    public void Dispose() {
        _tcs.TrySetCanceled();
    }

    public async IAsyncEnumerator<WsHeaderWithMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
        while (!_ct.IsCancellationRequested) {
            WsHeaderWithMessage res;
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

