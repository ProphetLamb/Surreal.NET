using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace SurrealDB.Ws;

[EventSource(Guid = "03c50b03-e245-46e5-a99a-6eaa28990a41", Name = "WsReceiverDeflaterEventSource")]
public sealed class WsReceiverDeflaterEventSource : EventSource
{
    private WsReceiverDeflaterEventSource() { }

    public static WsReceiverDeflaterEventSource Log { get; } = new();

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MessageReceived(string? messageId) {
        if (IsEnabled()) {
            MessageReceivedCore(messageId);
        }
    }

    [Event(1, Level = EventLevel.Verbose, Message = "Message (Id = {0}) pulled from channel")]
    private void MessageReceivedCore(string? messageId) => WriteEvent(1, messageId);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MessageDiscarded(string? messageId) {
        if (IsEnabled()) {
            MessageDiscardedCore(messageId);
        }
    }

    [Event(2, Level = EventLevel.Warning, Message = "No handler registered for the message (Id = {0})")]
    private void MessageDiscardedCore(string? messageId) => WriteEvent(2, messageId);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HandlerUnregisteredAfterException(string handlerId, Exception ex) {
        if (IsEnabled()) {
            HandlerUnregisteredAfterExceptionCore(handlerId, ex.ToString());
        }
    }

    [Event(3, Level = EventLevel.Error, Message = "The handler (Id = {0}) threw an exception during dispatch, and was unregistered. ERROR: {1}")]
    private unsafe void HandlerUnregisteredAfterExceptionCore(string handlerId, string ex) {
        WriteEvent(3, handlerId, ex);
    }

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HandlerUnregisterdFleeting(string handlerId) {
        if (IsEnabled()) {
            HandlerUnregisterdFleetingCore(handlerId);
        }
    }

    [Event(4, Level = EventLevel.Verbose, Message = "The handler (Id = {0}) is fleeting and was unregistered after dispatch")]
    private void HandlerUnregisterdFleetingCore(string handlerId) => WriteEvent(4, handlerId);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MessageAwaiting() {
        if (IsEnabled()) {
            MessageAwaitingCore();
        }
    }

    [Event(5, Level = EventLevel.Verbose, Message = "Waiting for message to pull from channel")]
    private void MessageAwaitingCore() => WriteEvent(5);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Opened() {
        if (IsEnabled()) {
            OpenedCore();
        }
    }

    [Event(6, Level = EventLevel.Informational, Message = "Opened and is now pulling from the channel")]
    private void OpenedCore() => WriteEvent(6);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseBegin() {
        if (IsEnabled()) {
            CloseBeginCore();
        }
    }

    [Event(7, Level = EventLevel.Informational, Message = "Closed and stopped pulling from the channel")]
    private void CloseBeginCore() => WriteEvent(7);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseFinish() {
        if (IsEnabled()) {
            CloseFinishCore();
        }
    }

    [Event(8, Level = EventLevel.Informational, Message = "Closing has finished")]
    private void CloseFinishCore() => WriteEvent(8);

    [NonEvent, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disposed() {
        if (IsEnabled()) {
            DisposedCore();
        }
    }

    [Event(9, Level = EventLevel.Informational, Message = "Disposed")]
    private void DisposedCore() => WriteEvent(9);
}
