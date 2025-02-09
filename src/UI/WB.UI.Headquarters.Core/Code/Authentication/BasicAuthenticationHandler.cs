﻿using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using WB.Core.BoundedContexts.Headquarters.Views.User;
using WB.Core.Infrastructure.Domain;
using WB.UI.Shared.Web.Authentication;

namespace WB.UI.Headquarters.Code.Authentication
{
    public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationSchemeOptions>
    {
        private const string FailureMessage = "Incorrect user name or password";
        private readonly SignInManager<HqUser> signInManager;
        private readonly IUserClaimsPrincipalFactory<HqUser> claimFactory;
        private readonly IInScopeExecutor executor;
        private bool isUserLocked;

        public BasicAuthenticationHandler(
            IOptionsMonitor<BasicAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            SignInManager<HqUser> signInManager,
            IUserClaimsPrincipalFactory<HqUser> claimFactory,
            IInScopeExecutor executor) : base(options, logger, encoder, clock)
        {
            this.signInManager = signInManager;
            this.claimFactory = claimFactory;
            this.executor = executor;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.NoResult();

            BasicCredentials creds;
            try
            {
                creds = Request.Headers.ParseBasicCredentials();

                return await executor.ExecuteAsync(async (sl) =>
                {
                    var signinManager = sl.GetInstance<SignInManager<HqUser>>();
                    
                    var user = await signinManager.UserManager.FindByNameAsync(creds.Username);
                    if (user == null) return AuthenticateResult.Fail(FailureMessage);                    
                    
                    var result = await signinManager.CheckPasswordSignInAsync(user, creds.Password, true);

                    if (result.IsLockedOut)
                        return AuthenticateResult.Fail("User is locked");
                    if (result.Succeeded)
                    {                         
                        if (user.IsArchivedOrLocked)
                        {
                            this.isUserLocked = true;
                            return AuthenticateResult.Fail("User is locked");
                        }
                        var principal = await this.claimFactory.CreateAsync(user);
                        var ticket = new AuthenticationTicket(principal, Scheme.Name);
    
                        return AuthenticateResult.Success(ticket);
                    }
                    else
                        return AuthenticateResult.Fail(FailureMessage);                    
                });
            }
            catch (Exception e)
            {
                return AuthenticateResult.Fail(e.Message);
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            HandleFail();
            Response.StatusCode = StatusCodes.Status401Unauthorized;

            if (this.isUserLocked)
            {
                await using StreamWriter bodyWriter = new StreamWriter(Response.Body);
                await bodyWriter.WriteAsync(JsonConvert.SerializeObject(new { Message = "User is locked" }));
            }
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            HandleFail();
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        private void HandleFail()
        {
            Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm}\", charset=\"UTF-8\"";
        }
    }
}
