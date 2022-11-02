using System.Diagnostics.Tracing;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace SurrealDB.Ws;

[EventSource(Guid = "91a1c84b-f0aa-43c8-ad21-6ff518a8fa01", Name = "WsReceiverInflaterEventSource")]
public sealed class WsReceiverInflaterEventSource : EventSource {
    private WsReceiverInflaterEventSource() { }

    public static WsReceiverInflaterEventSource Log { get; } = new();

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SocketWaiting() {
        if (IsEnabled()) {
            SocketWaitingCore();
        }
    }

    [Event(1, Level = EventLevel.Verbose, Message = "Waiting to receive a block from the socket")]
    private void SocketWaitingCore() => WriteEvent(1);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SockedReceived(WebSocketReceiveResult result) {
        if (IsEnabled()) {
            SockedReceivedCore(result.Count, result.EndOfMessage, result.CloseStatus is not null);
        }
    }

    [ThreadStatic]
    private static object[]? _socketReceivedArgs;
    [Event(2, Level = EventLevel.Verbose, Message = "Received a block from the socket (Count = {0}, EndOfMessage = {1}, Closed = {2})")]
    private unsafe void SockedReceivedCore(int count, bool endOfMessage, bool closed) {
        _socketReceivedArgs ??= new object[3];
        _socketReceivedArgs[0] = count;
        _socketReceivedArgs[1] = endOfMessage;
        _socketReceivedArgs[2] = closed;
        WriteEvent(2, _socketReceivedArgs);
    }

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MessageReceiveFinished() {
        if (IsEnabled()) {
            MessageReceiveFinishedCore();
        }
    }

    [Event(3, Level = EventLevel.Verbose, Message = "Finished receiving the message from the socket")]
    private void MessageReceiveFinishedCore() => WriteEvent(3);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MessagePushed() {
        if (IsEnabled()) {
            MessagePushedCore();
        }
    }

    [Event(4, Level = EventLevel.Informational, Message = "Pushed the message to the channel")]
    private void MessagePushedCore() => WriteEvent(4);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Opened() {
        if (IsEnabled()) {
            OpenedCore();
        }
    }

    [Event(5, Level = EventLevel.Informational, Message = "Opened and is now pushing to the channel")]
    private void OpenedCore() => WriteEvent(5);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseBegin() {
        if (IsEnabled()) {
            CloseBeginCore();
        }
    }

    [Event(6, Level = EventLevel.Informational, Message = "Closed and stopped pushing to the channel")]
    private void CloseBeginCore() => WriteEvent(6);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseFinish() {
        if (IsEnabled()) {
            CloseFinishCore();
        }
    }

    [Event(7, Level = EventLevel.Informational, Message = "Closing has finished")]
    private void CloseFinishCore() => WriteEvent(7);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disposed() {
        if (IsEnabled()) {
            DisposedCore();
        }
    }

    [Event(8, Level = EventLevel.Informational, Message = "Disposed")]
    private void DisposedCore() => WriteEvent(8);
}
