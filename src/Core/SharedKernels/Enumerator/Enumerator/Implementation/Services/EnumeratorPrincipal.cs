using System;
using WB.Core.GenericSubdomains.Portable;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;

namespace WB.Core.SharedKernels.Enumerator.Implementation.Services
{
    public abstract class EnumeratorPrincipal : IPrincipal
    {
        protected readonly IPasswordHasher passwordHasher;
        protected readonly ILogger Logger;

        protected EnumeratorPrincipal(IPasswordHasher passwordHasher, ILogger logger)
        {
            this.passwordHasher = passwordHasher;
            this.Logger = logger;
            Logger.Trace($"Creating Principal");
        }
        protected abstract IUserIdentity GetUserByName(string userName);
        protected abstract IUserIdentity GetUserById(string userId);

        public bool IsAuthenticated => this.currentUserIdentity != null;

        private IUserIdentity identity;
        protected IUserIdentity currentUserIdentity
        {
            private set
            {
                Logger.Trace($"Principal is setting. Value is null:{value == null}");
                identity = value;
            }
            get => identity;
        }
        IUserIdentity IPrincipal.CurrentUserIdentity => this.currentUserIdentity;

        private IUserIdentity FindIdentityByUsername(string userName) 
            => userName == null ? null : this.GetUserByName(userName.ToLower());

        public bool SignIn(string userName, string password, bool staySignedIn)
        {
            this.currentUserIdentity = null;

            var localUser = FindIdentityByUsername(userName); // db query

            if (localUser == null) return false;

            if (localUser.PasswordHash != null)
            {
                var verificationResult = this.passwordHasher.VerifyPassword(localUser.PasswordHash, password);
                if (verificationResult == PasswordVerificationResult.Failed)
                {
                    return false;
                }

                if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    UpdateUserHash(localUser.Id, this.passwordHasher.Hash(password));
                }

                this.currentUserIdentity = localUser;
            }

            return this.IsAuthenticated;
        }

        protected abstract void UpdateUserHash(string userId, string hash);

        public bool SignInWithHash(string userName, string passwordHash, bool staySignedIn)
        {
            this.currentUserIdentity = null;

            var localUser = FindIdentityByUsername(userName); // db query

            if (string.Equals(localUser.PasswordHash, passwordHash, StringComparison.Ordinal))
            {
                this.currentUserIdentity = localUser;
            }

            return this.IsAuthenticated;
        }

        public void SignOut() => this.currentUserIdentity = null;

        public bool SignIn(string userId, bool staySignedIn)
        {
            var interviewer = this.GetUserById(userId);
            this.currentUserIdentity = interviewer;
            return this.IsAuthenticated;
        }
    }
}
