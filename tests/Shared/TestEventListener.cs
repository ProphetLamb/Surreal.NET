using System.Diagnostics.Tracing;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SurrealDB.Shared.Tests;

public class TestEventListener : EventListener {
    private StreamWriter? _writer = new(File.OpenWrite($"./{nameof(TestEventListener)}_{DateTimeOffset.UtcNow:s}.log"));

    public TestEventListener() {
        AppDomain.CurrentDomain.FirstChanceException += FirstChanceException;
    }

    private void FirstChanceException(object? sender, FirstChanceExceptionEventArgs e) {
        ValueStringBuilder sb = new(stackalloc char[512]);
        sb.Append(DateTime.UtcNow.ToString("T"));
        sb.Append(" Error: ");
        sb.Append(e.Exception.ToString());

        WriteLine(sb.ToString());
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData) {
        string message = $"{eventData.TimeStamp:T} {eventData.EventSource.Name}: {eventData.Message} (E:{eventData.EventName}, T:{eventData.OSThreadId})";
        if (eventData.Payload is null) {
            WriteLine(message);
        } else {
            object?[] parameters = eventData.Payload.ToArray();
            WriteLine(message, parameters);
        }
        base.OnEventWritten(eventData);
    }

    private void WriteLine(string message) {
        _writer?.WriteLine(message);
    }

    private void WriteLine(string format, object?[] args) {
        _writer?.WriteLine(format, args);
    }

    public override void Dispose() {
        var writer = Interlocked.Exchange(ref _writer, null);
        if (writer is not null) {
            writer.Dispose();
            AppDomain.CurrentDomain.FirstChanceException -= FirstChanceException;
        }
        base.Dispose();
    }
}
