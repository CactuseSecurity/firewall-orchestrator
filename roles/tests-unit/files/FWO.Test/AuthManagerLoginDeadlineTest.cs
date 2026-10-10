using System.Diagnostics;
using System.Security.Cryptography;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The login deadline covers the whole directory work of a login, not only the authentication: a directory that
    /// stops answering during the group or role lookups must not hold the request (SEC-13).
    /// Not parallelizable: the search user password is decrypted with a test main key set for the whole process.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class AuthManagerLoginDeadlineTest
    {
        private const string kUserDn = "uid=slowuser,ou=users,dc=example,dc=com";
        private const string kGroupSearchPath = "ou=groups,dc=example,dc=com";
        private const string kRoleSearchPath = "ou=roles,dc=example,dc=com";
        private const int kLdapId = 21;
        private static readonly TimeSpan kShortLoginTimeout = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan kLongLoginTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan kRequestEnd = TimeSpan.FromMilliseconds(200);
        // well below the default deadline of 10 seconds, so only the configured deadline can have ended the login
        private static readonly TimeSpan kMaxDuration = TimeSpan.FromSeconds(5);
        private static readonly string[] kUserCn = ["slowuser"];
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");

        [TestCase(kGroupSearchPath)]
        [TestCase(kRoleSearchPath)]
        public void Login_WhenTheDirectoryStopsAnsweringDuringALookup_IsRefusedAtTheDeadline(string silentSearchPath)
        {
            using IDisposable mainKey = LdapTestSupport.UseTestMainKey();
            AuthManager authManager = CreateAuthManager(CreateDirectoryNotAnsweringSearchesIn(silentSearchPath), kShortLoginTimeout);
            Stopwatch stopwatch = Stopwatch.StartNew();

            LoginCapacityException? exception = Assert.ThrowsAsync<LoginCapacityException>(async () =>
                await authManager.AuthenticateAndBuildUserAsync(CreateUser(), validatePassword: false, updateLoginState: false));

            stopwatch.Stop();
            Assert.Multiple(() =>
            {
                Assert.That(exception?.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
                Assert.That(exception?.Message, Is.EqualTo(LoginCapacityException.kUnavailable));
                Assert.That(stopwatch.Elapsed, Is.LessThan(kMaxDuration));
            });
        }

        [TestCase(kGroupSearchPath)]
        [TestCase(kRoleSearchPath)]
        public void Login_WhenTheRequestEndsDuringALookup_IsCancelledInsteadOfRefused(string silentSearchPath)
        {
            using IDisposable mainKey = LdapTestSupport.UseTestMainKey();
            AuthManager authManager = CreateAuthManager(CreateDirectoryNotAnsweringSearchesIn(silentSearchPath), kLongLoginTimeout);
            using CancellationTokenSource request = new(kRequestEnd);
            Stopwatch stopwatch = Stopwatch.StartNew();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await authManager.AuthenticateAndBuildUserAsync(CreateUser(), validatePassword: false, updateLoginState: false, request.Token));

            stopwatch.Stop();
            Assert.That(stopwatch.Elapsed, Is.LessThan(kMaxDuration));
        }

        private static UiUser CreateUser()
        {
            return new() { Name = "slowuser", Dn = kUserDn, LdapConnection = new() { Id = kLdapId } };
        }

        private static AuthManager CreateAuthManager(RecordingLdapClient directory, TimeSpan loginTimeout)
        {
            TestableLdap ldap = new(directory)
            {
                Id = kLdapId,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                UserSearchPath = "ou=users,dc=example,dc=com",
                GroupSearchPath = kGroupSearchPath,
                RoleSearchPath = kRoleSearchPath,
                TenantId = 1
            };
            return new AuthManager(new JwtWriter(new RsaSecurityKey(RSA.Create(2048))), new List<Ldap> { ldap }, new SimulatedApiConnection())
            {
                LoginTimeout = loginTimeout
            };
        }

        /// <summary>
        /// A directory that finds the user, answers the searches in the other paths with nothing and never sends the
        /// results of a search in <paramref name="silentSearchPath"/>.
        /// </summary>
        private static RecordingLdapClient CreateDirectoryNotAnsweringSearchesIn(string silentSearchPath)
        {
            RecordingLdapClient client = new()
            {
                SearchResponder = (baseDn, scope, filter, attributes, typesOnly) => baseDn == silentSearchPath
                    ? new SilentSearchResults()
                    : LdapTestSupport.CreateSearchResults()
            };
            client.ReadResultsByDn[kUserDn] = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("cn", kUserCn));
            return client;
        }

        /// <summary>
        /// Search results that never arrive; waiting for them only ends by cancellation.
        /// </summary>
        private sealed class SilentSearchResults : ILdapSearchResults
        {
            public LdapControl[] ResponseControls { get; } = Array.Empty<LdapControl>();

            public async Task<bool> HasMoreAsync(CancellationToken ct = default)
            {
                await Task.Delay(Timeout.Infinite, ct);
                return false;
            }

            public Task<LdapEntry> NextAsync(CancellationToken ct = default)
            {
                throw new InvalidOperationException("no search result arrives");
            }

            public async IAsyncEnumerator<LdapEntry> GetAsyncEnumerator(CancellationToken ct = default)
            {
                await Task.Delay(Timeout.Infinite, ct);
                yield break;
            }
        }
    }
}
