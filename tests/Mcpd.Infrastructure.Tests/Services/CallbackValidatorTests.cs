using FluentAssertions;
using Mcpd.Infrastructure.Services;
using Xunit;

namespace Mcpd.Infrastructure.Tests.Services;

public sealed class CallbackValidatorMatchTests
{
    [Fact]
    public void ExactMatch_Succeeds()
    {
        var uri = new Uri("https://app.contoso.com/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "https://app.contoso.com/oauth/callback")
            .Should().BeTrue();
    }

    [Fact]
    public void ExactMatch_TrailingSlash_Succeeds()
    {
        var uri = new Uri("https://app.contoso.com/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "https://app.contoso.com/oauth/callback/")
            .Should().BeTrue();
    }

    [Fact]
    public void ExactMatch_DifferentPath_Fails()
    {
        var uri = new Uri("https://app.contoso.com/other");
        CallbackValidator.MatchesPattern(uri, "https://app.contoso.com/oauth/callback")
            .Should().BeFalse();
    }

    [Fact]
    public void WildcardSubdomain_Succeeds()
    {
        var uri = new Uri("https://myapp.contoso.com/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "https://*.contoso.com/oauth/callback")
            .Should().BeTrue();
    }

    [Fact]
    public void WildcardSubdomain_NestedSubdomain_Fails()
    {
        var uri = new Uri("https://sub.myapp.contoso.com/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "https://*.contoso.com/oauth/callback")
            .Should().BeFalse();
    }

    [Fact]
    public void WildcardSubdomain_DifferentDomain_Fails()
    {
        var uri = new Uri("https://evil.example.com/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "https://*.contoso.com/oauth/callback")
            .Should().BeFalse();
    }

    [Fact]
    public void Localhost_AnyPort_Succeeds()
    {
        var uri = new Uri("http://localhost:8080/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "http://localhost:*/oauth/callback")
            .Should().BeTrue();
    }

    [Fact]
    public void Localhost_DifferentPath_Fails()
    {
        var uri = new Uri("http://localhost:8080/other");
        CallbackValidator.MatchesPattern(uri, "http://localhost:*/oauth/callback")
            .Should().BeFalse();
    }

    [Fact]
    public void Localhost_HttpsScheme_Fails()
    {
        var uri = new Uri("https://localhost:8080/oauth/callback");
        CallbackValidator.MatchesPattern(uri, "http://localhost:*/oauth/callback")
            .Should().BeFalse();
    }
}
