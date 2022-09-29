using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

using SurrealDB.Configuration;

namespace SurrealDB.Extensions.Service;

public class SurrealOptions
    : IOptions<SurrealOptions>, IConfigureOptions<SurrealOptions>, IValidateOptions<SurrealOptions>, IPostConfigureOptions<SurrealOptions> {
    private bool _readonly;
    private Config _configuration;
    public Config Configuration {
        get => _configuration;
        set {
            if (_readonly) {
                ThrowReadonly();
            }
            _configuration = value;
        }
    }

    public SurrealOptions Value => this;

    public void Configure(SurrealOptions options) {
        options.Configuration = options.Configuration;
    }

    public ValidateOptionsResult Validate(string name, SurrealOptions options) {
        return options.Configuration.IsValidated
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Configuration is not marked as validated");
    }

    public void PostConfigure(string name, SurrealOptions options) {
        options._readonly = true;
    }

    [DebuggerStepThrough, DoesNotReturn]
    private static void ThrowReadonly() {
        throw new InvalidOperationException("The configuration is readonly!");
    }
}
