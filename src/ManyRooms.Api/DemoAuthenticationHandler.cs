using System.Security.Claims;
using System.Text.Encodings.Web;
using ManyRooms.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ManyRooms.Api;

public sealed class DemoAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IHostEnvironment environment, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentDemo";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Demo:Enabled"))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (Request.Headers["X-ManyRooms-Demo"].ToString() != "true")
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, DemoData.UserId.ToString()), new Claim(ClaimTypes.Name, "Alex Morgan")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
