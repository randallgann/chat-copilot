// Copyright (c) Microsoft. All rights reserved.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace CopilotChat.WebApi.Auth;

/// <summary>
/// Class implementing "authentication" that lets all requests pass through.
/// </summary>
public class PassThroughAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "PassThrough";
    private const string DefaultUserId = "c05c61eb-65e4-4223-915a-fe72b0c9ece1";
    private const string DefaultUserName = "Default User";

    // Header name for custom user ID
    private const string UserIdHeaderName = "X-User-Id";
    private const string UserNameHeaderName = "X-User-Name";

    /// <summary>
    /// Constructor
    /// </summary>
    public PassThroughAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder) : base(options, loggerFactory, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        this.Logger.LogInformation("Allowing request to pass through");

        // Check if the request contains custom user ID and name headers
        string userId = DefaultUserId;
        string userName = DefaultUserName;

        if (Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues) &&
            userIdValues.Count > 0 && !string.IsNullOrEmpty(userIdValues.FirstOrDefault()))
        {
            userId = userIdValues.FirstOrDefault()!;
            this.Logger.LogInformation("Using custom user ID from header: {UserId}", userId);
        }

        if (Request.Headers.TryGetValue(UserNameHeaderName, out var userNameValues) &&
            userNameValues.Count > 0 && !string.IsNullOrEmpty(userNameValues.FirstOrDefault()))
        {
            userName = userNameValues.FirstOrDefault()!;
            this.Logger.LogInformation("Using custom user name from header: {UserName}", userName);
        }

        Claim userIdClaim = new(ClaimConstants.Sub, userId);
        Claim nameClaim = new(ClaimConstants.Name, userName);
        ClaimsIdentity identity = new(new Claim[] { userIdClaim, nameClaim }, AuthenticationScheme);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, this.Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Returns true if the given user ID is the default user guest ID.
    /// </summary>
    public static bool IsDefaultUser(string userId) => userId == DefaultUserId;
}
