using System.Diagnostics.Tracing;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace SurrealDB.Ws;

[EventSource(Guid = "03c50b03-e245-46e5-a99a-6eaa28990a41", Name = "SurrealDB.Ws.WsReceiverDeflaterEventSource")]
public sealed class WsReceiverInflaterEventSource : EventSource
{
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

    [Event(2, Level = EventLevel.Verbose, Message = "Received a block from the socket (Count = {0], EndOfMessage = {1}, Closed = {2})")]
    private unsafe void SockedReceivedCore(int count, bool endOfMessage, bool closed) {
        EventData* payload = stackalloc EventData[3];
        payload[0].Size = sizeof(int);
        payload[0].DataPointer = (IntPtr)(&count);
        payload[1].Size = sizeof(bool);
        payload[1].DataPointer = (IntPtr)(&endOfMessage);
        payload[2].Size = sizeof(bool);
        payload[2].DataPointer = (IntPtr)(&closed);
        WriteEventCore(2, 3, payload);
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

    [Event(5, Level = EventLevel.Informational, Message = "Inflater opened and is now pushing to the channel")]
    private void OpenedCore() => WriteEvent(5);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseBegin() {
        if (IsEnabled()) {
            CloseBeginCore();
        }
    }

    [Event(6, Level = EventLevel.Informational, Message = "Inflater closed and stopped pushing to the channel")]
    private void CloseBeginCore() => WriteEvent(6);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseFinish() {
        if (IsEnabled()) {
            CloseFinishCore();
        }
    }

    [Event(7, Level = EventLevel.Informational, Message = "Inflater closing has finished")]
    private void CloseFinishCore() => WriteEvent(7);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disposed() {
        if (IsEnabled()) {
            DisposedCore();
        }
    }

    [Event(8, Level = EventLevel.Informational, Message = "Inflater disposed")]
    private void DisposedCore() => WriteEvent(8);
}
