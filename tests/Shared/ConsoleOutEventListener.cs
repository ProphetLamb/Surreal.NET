using System.Diagnostics.Tracing;

namespace SurrealDB.Shared.Tests;

public class ConsoleOutEventListener : EventListener {

    protected override void OnEventWritten(EventWrittenEventArgs eventData) {
        string message = $"{eventData.TimeStamp:T} - {eventData.EventSource.Name} - {eventData.EventName} - {eventData.OSThreadId}: {eventData.Message}";
        if (eventData.Payload is null) {
            Console.WriteLine(message);
        } else {
            Console.WriteLine(message, eventData.Payload.ToArray());
        }
        base.OnEventWritten(eventData);
    }
}
