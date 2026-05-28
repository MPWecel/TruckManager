using AwesomeAssertions;
using Serilog.Core;
using Serilog.Events;
using Xunit;

using TruckManager.Infrastructure.Logging;

namespace TruckManager.UnitTests.Infrastructure.Tests.Logging;

// Phase 7 / Section D.   Unit tests for SensitivePropertyDestructuringPolicy.
// Asserts the masking contract directly via TryDestructure — no Serilog logger / sink needed.
// Each masking test uses its own anonymous-type literal so the property name we want masked is visible at the test site (no reflection acrobatics, no duplicate-case-named members).
public class SensitivePropertyDestructuringPolicyTests
{
    private readonly SensitivePropertyDestructuringPolicy _policy = new();
    private readonly PassthroughPropertyValueFactory _factory = new();

    // ---- Property-name keyword detection ----------------------------------

    [Fact]
    public void TryDestructure_masks_Password()
        => AssertMasked(new { Username = "alice", Password = "hunter2" }, "Password");

    [Fact]
    public void TryDestructure_masks_lowercase_password_property()
        => AssertMasked(new { Username = "alice", password = "hunter2" }, "password");

    [Fact]
    public void TryDestructure_masks_UPPERCASE_PASSWORD_property()
        => AssertMasked(new { Username = "alice", PASSWORD = "hunter2" }, "PASSWORD");

    [Fact]
    public void TryDestructure_masks_property_containing_password()
        => AssertMasked(new { Username = "alice", UserPassword = "hunter2" }, "UserPassword");

    [Fact]
    public void TryDestructure_masks_Pwd()
        => AssertMasked(new { Pwd = "x", Other = 1 }, "Pwd");

    [Fact]
    public void TryDestructure_masks_Secret()
        => AssertMasked(new { Secret = "x", Other = 1 }, "Secret");

    [Fact]
    public void TryDestructure_masks_property_containing_secret()
        => AssertMasked(new { ApiSecret = "x", Other = 1 }, "ApiSecret");

    [Fact]
    public void TryDestructure_masks_Token()
        => AssertMasked(new { Token = "x", Other = 1 }, "Token");

    [Fact]
    public void TryDestructure_masks_AccessToken()
        => AssertMasked(new { AccessToken = "x", Other = 1 }, "AccessToken");

    [Fact]
    public void TryDestructure_masks_Authorization()
        => AssertMasked(new { Authorization = "Bearer abc", Other = 1 }, "Authorization");

    [Fact]
    public void TryDestructure_masks_ApiKey()
        => AssertMasked(new { ApiKey = "k-12345", Other = 1 }, "ApiKey");

    [Fact]
    public void TryDestructure_masks_ConnectionString()
        => AssertMasked(new { ConnectionString = "Host=...;Password=...", Other = 1 }, "ConnectionString");

    [Fact]
    public void TryDestructure_masks_Cookie()
        => AssertMasked(new { Cookie = "sessid=abc", Other = 1 }, "Cookie");

    [Fact]
    public void TryDestructure_masks_property_containing_cookie()
        => AssertMasked(new { SessionCookie = "abc", Other = 1 }, "SessionCookie");

    // ---- Fast path: nothing sensitive → returns false ----------------------

    [Fact]
    public void TryDestructure_returns_false_when_no_sensitive_property_present()
    {
        var harmless = new { Username = "alice", Age = 30, Bio = "hi" };

        bool ok = _policy.TryDestructure(harmless, _factory, out LogEventPropertyValue? result);

        ok.Should()
          .BeFalse();
        result.Should()
              .BeNull();
    }

    [Fact]
    public void TryDestructure_returns_false_for_scalar_types()
    {
        _policy.TryDestructure(42, _factory, out _).Should()
                                                   .BeFalse();
        _policy.TryDestructure("hello", _factory, out _).Should()
                                                        .BeFalse();
        _policy.TryDestructure(Guid.NewGuid(), _factory, out _).Should()
                                                               .BeFalse();
        _policy.TryDestructure(DateTime.UtcNow, _factory, out _).Should()
                                                                .BeFalse();
        _policy.TryDestructure(DateTimeOffset.UtcNow, _factory, out _).Should()
                                                                      .BeFalse();
    }

    // ---- Mixed: sensitive masked, non-sensitive preserved ------------------

    [Fact]
    public void TryDestructure_preserves_non_sensitive_properties_alongside_masked_ones()
    {
        var payload = new { Username = "alice", Password = "hunter2", Remember = true };

        bool ok = _policy.TryDestructure(payload, _factory, out LogEventPropertyValue? result);

        ok.Should()
          .BeTrue();
        StructureValue structureValue = result.Should()
                                              .BeOfType<StructureValue>()
                                              .Subject;

        ScalarValue username = (ScalarValue)structureValue.Properties.Single(p => p.Name == "Username").Value;
        ScalarValue password = (ScalarValue)structureValue.Properties.Single(p => p.Name == "Password").Value;
        ScalarValue remember = (ScalarValue)structureValue.Properties.Single(p => p.Name == "Remember").Value;

        username.Value.Should()
                      .Be("alice");
        password.Value.Should()
                      .Be(SensitivePropertyDestructuringPolicy.MaskedValue);
        remember.Value.Should()
                      .Be(true);
    }

    // ---- Helpers -----------------------------------------------------------

    private void AssertMasked(object instance, string sensitivePropertyName)
    {
        bool ok = _policy.TryDestructure(instance, _factory, out LogEventPropertyValue? result);

        ok.Should()
          .BeTrue();
        StructureValue structureValue = result.Should()
                                              .BeOfType<StructureValue>()
                                              .Subject;
        ScalarValue maskedValue = (ScalarValue)structureValue.Properties
                                                             .Single(p => p.Name == sensitivePropertyName)
                                                             .Value;
        maskedValue.Value.Should()
                         .Be(SensitivePropertyDestructuringPolicy.MaskedValue);
    }

    // Test stand-in for ILogEventPropertyValueFactory. The real factory delegates to other policies + the default reflection destructurer;
    // for this policy's unit tests, a ScalarValue wrapper is enough — we never inspect the non-sensitive properties' rendered form, only the masked ones.
    private sealed class PassthroughPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
            => new ScalarValue(value);
    }
}
