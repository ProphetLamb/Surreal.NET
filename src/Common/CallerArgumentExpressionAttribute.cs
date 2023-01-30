// ReSharper disable CheckNamespace
#if !NETCOREAPP3_0_OR_GREATER

#pragma warning disable IDE0130
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130

/// <summary>Indicates that a parameter captures the expression passed for another parameter as a string.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.</summary>
    /// <param name="parameterName">The name of the parameter whose expression should be captured as a string.</param>
    public CallerArgumentExpressionAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    /// <summary>Gets the name of the parameter whose expression should be captured as a string.</summary>
    public string ParameterName { get; }
}

#endif
