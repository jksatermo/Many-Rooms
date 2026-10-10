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
        var persona = Request.Headers["X-ManyRooms-Demo"].ToString();
        if (persona is not ("true" or "leader"))
            return Task.FromResult(AuthenticateResult.NoResult());
        // Only fixed fictional identities are selectable. Roles are checked against database membership,
        // never taken from a caller-supplied role or trusted merely because this header says "leader".
        var leader = persona == "leader";
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, (leader ? DemoData.LeaderUserId : DemoData.UserId).ToString()),
                new Claim(ClaimTypes.Name, leader ? "Jamie Chen" : "Alex Morgan")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
