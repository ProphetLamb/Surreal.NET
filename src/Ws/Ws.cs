using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using SurrealDB.Common;

namespace SurrealDB.Ws;

public sealed class Ws : IDisposable, IAsyncDisposable {
    private readonly CancellationTokenSource _cts = new();
    private readonly WsTx _tx = new();
    private readonly ConcurrentDictionary<string, IHandler> _handlers = new();
    private Task _recv = Task.CompletedTask;

    public bool Connected => _tx.Connected;

    public async Task Open(Uri remote, CancellationToken ct = default) {
        await _tx.Open(remote, ct);
        _recv = Task.Run(async () => await Receive(_cts.Token), _cts.Token);
    }

    public async Task Close(CancellationToken ct = default) {
        Task t1 = _tx.Close(ct);
        Task t2 = Task.Run(ClearHandlers, ct);
        _cts.Cancel();

        await t1;
        await t2;
    }

    /// <summary>
    /// Sends the request and awaits a response from the server
    /// </summary>
    public async Task<(WsTx.RspHeader rsp, WsTx.NtyHeader nty, Stream stm)> RequestOnce(string id, Stream request, CancellationToken ct = default) {
        ResponseHandler handler = new(id, ct);
        Register(handler);
        await _tx.Tw(request, ct);
        return await handler.Task;
    }

    /// <summary>
    /// Sends the request and awaits responses from the server until manually canceled using the cancellation token
    /// </summary>
    public async IAsyncEnumerable<(WsTx.RspHeader rsp, WsTx.NtyHeader nty, Stream stm)> RequestPersists(string id, Stream request, [EnumeratorCancellation] CancellationToken ct = default) {
        NotificationHandler handler = new(this, id, ct);
        Register(handler);
        await _tx.Tw(request, ct);
        await foreach (var res in handler) {
            yield return res;
        }
    }

    internal void Register(IHandler handler) {
        if (!_handlers.TryAdd(handler.Id, handler)) {
            ThrowDuplicateId(handler.Id);
        }
    }

    internal void Unregister(IHandler handler) {
        if (!_handlers.TryRemove(handler.Id, out var h)) {
            return;
        }

        h.Dispose();
    }

    private async Task Receive(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            var (id, response, notify, stream) = await _tx.Tr(stoppingToken);

            stoppingToken.ThrowIfCancellationRequested();

            if (String.IsNullOrEmpty(id)) {
                continue; // Invalid response
            }

            if (!_handlers.TryGetValue(id, out IHandler? handler)) {
                // assume that unhandled responses belong to other clients
                // discard!
                await stream.DisposeAsync();
                continue;
            }

            handler.Handle(response, notify, stream);

            if (!handler.Persistent) {
                // persistent handlers are for notifications and are not removed automatically
                Unregister(handler);
            }

            if (stream is WsStream wsStream) {
                while (!wsStream.EndOfMessage) {
                    await Task.Delay(13, stoppingToken);
                }
            }
        }
    }

    private void ClearHandlers() {
        foreach (var handler in _handlers.Values) {
            Unregister(handler);
        }
    }

    public void Dispose() {
        try {
            Close().Wait();
            _tx.Dispose();
        } catch (OperationCanceledException) {
            // expected
        } catch (AggregateException) {
            // wrapping OperationCanceledException
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await Close();
            _tx.Dispose();
        } catch (OperationCanceledException) {
            // expected
        } catch (AggregateException) {
            // wrapping OperationCanceledException for async
        }
    }

    [DoesNotReturn]
    private static void ThrowDuplicateId(string id) {
        throw new ArgumentOutOfRangeException(nameof(id), $"A request with the Id `{id}` is already registered");
    }
}
