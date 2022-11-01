using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace SurrealDB.Common;

public abstract record ValidateReadonly : LatentReadonly {
    /// <summary>Evaluates <see cref="Validations"/>, if it yields any errors, throws a <see cref="AggregatePropertyValidationException"/> with the errors</summary>
    protected void ValidateOrThrow(string? message = null) {
        PoolArrayBuilder<(string, string)> errors = new();
        foreach (var error in Validations()) {
            errors.Append(error);
        }

        if (!errors.IsDefault) {
            AggregatePropertyValidationException.Throw(message, errors.ToArray());
        }
    }

    /// <summary>Performs a sequence of validation on properties.
    /// If a validation fails, yields the PropertyName and the corresponding error message.</summary>
    protected abstract IEnumerable<(string PropertyName, string Message)> Validations();
}

[Serializable]
public class AggregatePropertyValidationException : Exception {
    public AggregatePropertyValidationException() {
    }

    public AggregatePropertyValidationException(string? message) : base(message) {
    }

    public AggregatePropertyValidationException(string? message, Exception? inner) : base(message, inner) {
    }

    public AggregatePropertyValidationException(string? message, (string Property, string Error)[]? errors, Exception? inner) : base(message, inner) {
        Errors = errors;
    }

    protected AggregatePropertyValidationException(
        SerializationInfo info,
        StreamingContext context) : base(info, context) {
    }

    public (string Property, string Error)[]? Errors { get; set; }

    public override string ToString() {
        ValueStringBuilder sb = new(stackalloc char[512]);
        Span<(string PropertyName, string Message)>.Enumerator en = Errors.AsSpan().GetEnumerator();
        sb.Append(Message ?? "Validation failed with the following errors:");
        if (en.MoveNext()) {
            sb.Append(Environment.NewLine);
            sb.Append("- `");
            sb.Append(en.Current.PropertyName);
            sb.Append("`: ");
            sb.Append(en.Current.Message);
        }
        while (en.MoveNext()) {
            sb.Append(Environment.NewLine);
            sb.Append("- `");
            sb.Append(en.Current.PropertyName);
            sb.Append("`: ");
            sb.Append(en.Current.Message);
        }

        return sb.ToString();
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(string? message, (string, string)[]? errors = null, Exception? inner = default) {
        throw new AggregatePropertyValidationException(message, errors, inner);
    }
}
