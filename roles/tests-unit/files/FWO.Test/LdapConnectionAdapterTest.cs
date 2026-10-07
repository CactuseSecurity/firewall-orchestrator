using System;
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
                using NovellLdapConnectionAdapter adapter = new(connection, cancellation.Token);

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
                NovellLdapConnectionAdapter adapter = new(connection, cancellation.Token);

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
    }
}
