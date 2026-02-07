using FluentAssertions;
using Mcpd.Application.Commands;
using Mcpd.Application.Validators;
using Xunit;

namespace Mcpd.Application.Tests.Validators;

public sealed class RegisterClientCommandValidatorTests
{
    private readonly RegisterClientCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_Passes()
    {
        var command = CreateValidCommand();
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_ClientName_Fails()
    {
        var command = CreateValidCommand() with { ClientName = "" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientName");
    }

    [Fact]
    public void ClientName_ExceedsMaxLength_Fails()
    {
        var command = CreateValidCommand() with { ClientName = new string('a', 257) };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_RequestedServerIds_Fails()
    {
        var command = CreateValidCommand() with { RequestedServerIds = [] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_RedirectUris_Fails()
    {
        var command = CreateValidCommand() with { RedirectUris = [] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_RedirectUri_Fails()
    {
        var command = CreateValidCommand() with { RedirectUris = ["not-a-uri"] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Http_NonLocalhost_RedirectUri_Fails()
    {
        var command = CreateValidCommand() with { RedirectUris = ["http://example.com/callback"] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Http_Localhost_RedirectUri_Passes()
    {
        var command = CreateValidCommand() with { RedirectUris = ["http://localhost:8080/callback"] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fragment_In_RedirectUri_Fails()
    {
        var command = CreateValidCommand() with { RedirectUris = ["https://example.com/callback#fragment"] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_GrantType_Fails()
    {
        var command = CreateValidCommand() with { GrantTypes = ["implicit"] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_AuthMethod_Fails()
    {
        var command = CreateValidCommand() with { TokenEndpointAuthMethod = "private_key_jwt" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    private static RegisterClientCommand CreateValidCommand() => new(
        "Test Client",
        ["https://example.com/callback"],
        ["client_credentials"],
        "client_secret_post",
        [Guid.NewGuid()],
        new Dictionary<Guid, string[]>());
}
