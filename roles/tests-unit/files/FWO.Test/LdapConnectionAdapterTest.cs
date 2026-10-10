using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LdapConnectionAdapterTest
    {
        private static readonly string[] kNoAttributes = Array.Empty<string>();
        private static readonly LdapModification[] kNoModifications = Array.Empty<LdapModification>();
        private static readonly string kUserDn = "uid=user,ou=users,dc=example,dc=com";

        [Test]
        public void BoundAndConstraintsExposeWrappedConnectionState()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.Multiple(() =>
            {
                Assert.That(adapter.Bound, Is.False);
                Assert.That(adapter.SearchConstraints, Is.InstanceOf<LdapSearchConstraints>());
                Assert.That(adapter.Constraints, Is.InstanceOf<LdapConstraints>());
            });
        }

        [Test]
        public void ConstraintsSetter_ForwardsToWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());
            LdapConstraints constraints = new()
            {
                ReferralFollowing = true
            };

            adapter.Constraints = constraints;

            Assert.That(adapter.Constraints.ReferralFollowing, Is.True);
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.DoesNotThrow(() => adapter.Dispose());
        }

        [Test]
        public async Task BindAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.BindAsync("cn=bind,dc=example,dc=com", "secret"), Throws.Exception);
        }

        [Test]
        public async Task ReadAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.ReadAsync(kUserDn), Throws.Exception);
        }

        [Test]
        public async Task SearchAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(
                async () => await adapter.SearchAsync("ou=users,dc=example,dc=com", LdapConnection.ScopeSub, "(uid=user)", kNoAttributes, false),
                Throws.Exception);
        }

        [Test]
        public async Task AddAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());
            LdapEntry entry = new("uid=user,ou=users,dc=example,dc=com", new LdapAttributeSet());

            Assert.That(async () => await adapter.AddAsync(entry), Throws.Exception);
        }

        [Test]
        public async Task DeleteAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.DeleteAsync(kUserDn), Throws.Exception);
        }

        [Test]
        public async Task ModifyAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.ModifyAsync(kUserDn, kNoModifications), Throws.Exception);
        }

        [Test]
        public async Task RenameAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.RenameAsync(kUserDn, "uid=user2", true), Throws.Exception);
        }

        private static readonly TimeSpan kCancelAfter = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan kWaitLimit = TimeSpan.FromSeconds(10);
        private const int kConnectionTimeoutMs = 3000;
        private const int kReadBufferSize = 1024;
        private const int kRaceAttempts = 10;
        private const int kMaxCancelDelayMicroseconds = 300;
        private static readonly TimeSpan kTestOperationTimeLimit = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan kCancelReturnLimit = TimeSpan.FromSeconds(1);

        /// <summary>
        /// A single constraint update limits both ordinary operations and searches without losing search settings.
        /// </summary>
        [Test]
        public void OperationTimeLimit_UpdatesBothConstraintViewsAndPreservesSearchSettings()
        {
            using LdapConnection connection = new();
            LdapSearchConstraints original = connection.SearchConstraints;
            original.MaxResults = kReadBufferSize;
            original.ReferralFollowing = true;
            connection.Constraints = original;

            using NovellLdapConnectionAdapter adapter = new(connection, kTestOperationTimeLimit);

            Assert.Multiple(() =>
            {
                Assert.That(connection.Constraints.TimeLimit, Is.EqualTo(kTestOperationTimeLimit.TotalMilliseconds));
                Assert.That(connection.SearchConstraints.TimeLimit, Is.EqualTo(kTestOperationTimeLimit.TotalMilliseconds));
                Assert.That(connection.SearchConstraints.MaxResults, Is.EqualTo(original.MaxResults));
                Assert.That(connection.SearchConstraints.ReferralFollowing, Is.True);
            });
        }

        /// <summary>
        /// Reads what the client sent until the client closes the connection.
        /// </summary>
        private static async Task<bool> IsClosedByClientAsync(TcpClient server)
        {
            using CancellationTokenSource timeout = new(kWaitLimit);
            byte[] buffer = new byte[kReadBufferSize];
            NetworkStream stream = server.GetStream();
            try
            {
                while (await stream.ReadAsync(buffer, timeout.Token) > 0)
                {
                    // discard the bind request
                }
                return true;
            }
            catch (IOException)
            {
                return true; // connection reset by the client
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        [Test]
        public async Task Cancellation_ClosesTheConnectionAndEndsAWaitingBind()
        {
            // a directory that accepts the connection but never answers: Novell's bind waits without observing the token
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
                LdapConnection connection = new() { ConnectionTimeout = kConnectionTimeoutMs };
                await connection.ConnectAsync(IPAddress.Loopback.ToString(), port);
                using TcpClient server = await accepted;
                using CancellationTokenSource cancellation = new();
                using NovellLdapConnectionAdapter adapter = new(connection, cancellationToken: cancellation.Token);

                // the deadline is armed first, as Novell may block the calling thread until the answer arrives
                cancellation.CancelAfter(kCancelAfter);
                Task bind = Task.Run(() => adapter.BindAsync("uid=user,dc=example,dc=com", "secret", cancellation.Token));
                Task finished = await Task.WhenAny(bind, Task.Delay(kWaitLimit));

                Assert.That(finished, Is.SameAs(bind), "the bind kept waiting after the cancellation");
                Assert.That(bind.IsFaulted || bind.IsCanceled, Is.True);
                Assert.That(await IsClosedByClientAsync(server), Is.True);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task Dispose_ClosesTheConnectionOnlyOnceAndIgnoresALaterCancellation()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
                LdapConnection connection = new() { ConnectionTimeout = kConnectionTimeoutMs };
                await connection.ConnectAsync(IPAddress.Loopback.ToString(), port);
                using TcpClient server = await accepted;
                using CancellationTokenSource cancellation = new();
                NovellLdapConnectionAdapter adapter = new(connection, cancellationToken: cancellation.Token);

                adapter.Dispose();
                adapter.Dispose();

                Assert.DoesNotThrow(cancellation.Cancel);
                Assert.That(await IsClosedByClientAsync(server), Is.True);
            }
            finally
            {
                listener.Stop();
            }
        }


        [Test]
        public async Task Cancellation_DuringTheStartOfABind_NeitherBlocksTheCancellerNorLeavesTheBindWaiting()
        {
            // cancelling while Novell is still sending the bind used to block the canceller in Dispose and the bind
            // forever; the close runs on the thread pool and the time limit ends what still waits
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start(kRaceAttempts);
            List<TcpClient> servers = [];
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                for (int attempt = 0; attempt < kRaceAttempts; attempt++)
                {
                    Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
                    LdapConnection connection = new() { ConnectionTimeout = kConnectionTimeoutMs };
                    await connection.ConnectAsync(IPAddress.Loopback.ToString(), port);
                    servers.Add(await accepted);
                    using CancellationTokenSource cancellation = new();
                    using NovellLdapConnectionAdapter adapter = new(connection, kTestOperationTimeLimit, cancellation.Token);
                    int cancelDelay = Random.Shared.Next(kMaxCancelDelayMicroseconds);

                    Task bind = Task.Run(() => adapter.BindAsync("uid=user,dc=example,dc=com", "secret", cancellation.Token));
                    Task cancel = Task.Run(() =>
                    {
                        Stopwatch delay = Stopwatch.StartNew();
                        while (delay.Elapsed.TotalMicroseconds < cancelDelay)
                        {
                            Thread.SpinWait(1);
                        }
                        cancellation.Cancel();
                    });

                    Assert.That(await Task.WhenAny(cancel, Task.Delay(kCancelReturnLimit)), Is.SameAs(cancel), "Cancel() blocked");
                    Assert.That(await Task.WhenAny(bind, Task.Delay(kWaitLimit)), Is.SameAs(bind), "the bind kept waiting");
                }
            }
            finally
            {
                servers.ForEach(server => server.Dispose());
                listener.Stop();
            }
        }

        [Test]
        public async Task OperationTimeLimit_EndsABindTheDirectoryNeverAnswers()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
                LdapConnection connection = new() { ConnectionTimeout = kConnectionTimeoutMs };
                await connection.ConnectAsync(IPAddress.Loopback.ToString(), port);
                using TcpClient server = await accepted;
                using NovellLdapConnectionAdapter adapter = new(connection, kTestOperationTimeLimit, CancellationToken.None);

                Task bind = Task.Run(() => adapter.BindAsync("uid=user,dc=example,dc=com", "secret", CancellationToken.None));

                Assert.That(await Task.WhenAny(bind, Task.Delay(kWaitLimit)), Is.SameAs(bind), "the bind kept waiting");
                Assert.That(bind.IsFaulted, Is.True);
                Assert.That(bind.Exception?.InnerException, Is.InstanceOf<LdapException>());
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
