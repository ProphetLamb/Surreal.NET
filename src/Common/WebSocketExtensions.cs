using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace SurrealDB.Common;

internal static class WebSocketExtensions {
    /// <summary>Throws a <see cref="OperationCanceledException"/> if the result is a close ack.</summary>
    /// <param name="result"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfClose(this WebSocketReceiveResult result) {
        if (result.CloseStatus is not null) {
            ThrowConnectionClosed();
        }
    }

    [DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowConnectionClosed() {
        throw new OperationCanceledException("Connection closed");
    }
}
