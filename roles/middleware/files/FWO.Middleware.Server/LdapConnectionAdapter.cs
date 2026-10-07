using Novell.Directory.Ldap;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Minimal LDAP client abstraction used by <see cref="Ldap"/> for testable connection handling.
    /// </summary>
    public interface ILdapClient : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the LDAP client is bound.
        /// </summary>
        bool Bound { get; }
        /// <summary>
        /// Gets the current search constraints.
        /// </summary>
        LdapConstraints SearchConstraints { get; }
        /// <summary>
        /// Gets or sets the current constraints.
        /// </summary>
        LdapConstraints Constraints { get; set; }
        /// <summary>
        /// Binds the client with the provided credentials.
        /// </summary>
        Task BindAsync(string user, string password);
        /// <summary>Binds with cancellation for authentication requests.</summary>
        Task BindAsync(string user, string password, CancellationToken cancellationToken) => BindAsync(user, password).WaitAsync(cancellationToken);
        /// <summary>
        /// Reads a single LDAP entry by distinguished name.
        /// </summary>
        Task<LdapEntry?> ReadAsync(string distinguishedName);
        /// <summary>Reads with cancellation for authentication requests.</summary>
        Task<LdapEntry?> ReadAsync(string distinguishedName, CancellationToken cancellationToken) => ReadAsync(distinguishedName).WaitAsync(cancellationToken);
        /// <summary>
        /// Executes an LDAP search.
        /// </summary>
        Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly);
        /// <summary>Searches with cancellation for authentication requests.</summary>
        Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly, CancellationToken cancellationToken)
            => SearchAsync(baseDn, scope, filter, attributes, typesOnly).WaitAsync(cancellationToken);
        /// <summary>
        /// Adds an LDAP entry.
        /// </summary>
        Task AddAsync(LdapEntry entry);
        /// <summary>
        /// Deletes an LDAP entry by distinguished name.
        /// </summary>
        Task DeleteAsync(string distinguishedName);
        /// <summary>
        /// Modifies an LDAP entry.
        /// </summary>
        Task ModifyAsync(string distinguishedName, LdapModification[] mods);
        /// <summary>
        /// Renames an LDAP entry.
        /// </summary>
        Task RenameAsync(string distinguishedName, string newRdn, bool deleteOldRdn);
    }

    internal sealed class NovellLdapConnectionAdapter : ILdapClient
    {
        private readonly LdapConnection connection;
        private readonly CancellationTokenRegistration cancellationRegistration;
        private int disposed;

        /// <summary>
        /// Wraps a connected Novell connection.
        /// </summary>
        /// <remarks>
        /// Novell waits for the answer to a bind, read or search without observing the cancellation token. So the
        /// connection is closed when the token is cancelled, which ends most waiting operations: otherwise an abandoned
        /// login would hold its LDAP slot until the directory answers (see LdapAuthenticationGate).
        /// The connection is closed on a thread pool thread, not in the cancellation callback: closing it while a bind is
        /// still being sent can block until the directory answers, which would block whoever cancels (the deadline timer
        /// or the request). Some operations still wait after the close, so the time limit ends them as a backstop.
        /// </remarks>
        /// <param name="connection">The connected Novell connection.</param>
        /// <param name="cancellationToken">Closes the connection when cancelled; default for connections without a deadline.</param>
        /// <param name="operationTimeLimit">Longest time a single operation waits for an answer; null for no limit.</param>
        internal NovellLdapConnectionAdapter(LdapConnection connection, CancellationToken cancellationToken = default, TimeSpan? operationTimeLimit = null)
        {
            this.connection = connection;
            if (operationTimeLimit is TimeSpan timeLimit)
            {
                ApplyOperationTimeLimit(connection, timeLimit);
            }
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(
                    static adapter => ThreadPool.UnsafeQueueUserWorkItem(static state => state.Dispose(), (NovellLdapConnectionAdapter)adapter!, preferLocal: false),
                    this);
            }
        }

        /// <summary>
        /// Limits the time every bind, read and search of the connection waits for an answer of the directory.
        /// </summary>
        /// <param name="connection">The Novell connection.</param>
        /// <param name="timeLimit">Longest time to wait for an answer.</param>
        internal static void ApplyOperationTimeLimit(LdapConnection connection, TimeSpan timeLimit)
        {
            int milliseconds = (int)Math.Ceiling(timeLimit.TotalMilliseconds);
            LdapConstraints constraints = connection.Constraints;
            constraints.TimeLimit = milliseconds;
            connection.Constraints = constraints;
            LdapSearchConstraints searchConstraints = connection.SearchConstraints;
            searchConstraints.TimeLimit = milliseconds;
            connection.Constraints = searchConstraints;
        }

        public bool Bound => connection.Bound;

        public LdapConstraints SearchConstraints => connection.SearchConstraints;

        public LdapConstraints Constraints
        {
            get => (LdapConstraints)connection.Constraints;
            set => connection.Constraints = value;
        }

        // The overloads without a token serve operations without a deadline (administration, synchronization) and opt out
        // of cancellation explicitly; logins use the overloads with a token, see the constructor.
        public Task BindAsync(string user, string password)
        {
            return connection.BindAsync(user, password, CancellationToken.None);
        }

        /// <inheritdoc />
        public Task BindAsync(string user, string password, CancellationToken cancellationToken)
        {
            return connection.BindAsync(user, password, cancellationToken);
        }

        public Task<LdapEntry?> ReadAsync(string distinguishedName)
        {
            return connection.ReadAsync(distinguishedName, CancellationToken.None);
        }

        /// <inheritdoc />
        public Task<LdapEntry?> ReadAsync(string distinguishedName, CancellationToken cancellationToken)
        {
            return connection.ReadAsync(distinguishedName, cancellationToken);
        }

        public async Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly)
        {
            return await connection.SearchAsync(baseDn, scope, filter, attributes, typesOnly, CancellationToken.None);
        }

        /// <inheritdoc />
        public async Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly, CancellationToken cancellationToken)
        {
            return await connection.SearchAsync(baseDn, scope, filter, attributes, typesOnly, cancellationToken);
        }

        public Task AddAsync(LdapEntry entry)
        {
            return connection.AddAsync(entry, CancellationToken.None);
        }

        public Task DeleteAsync(string distinguishedName)
        {
            return connection.DeleteAsync(distinguishedName, CancellationToken.None);
        }

        public Task ModifyAsync(string distinguishedName, LdapModification[] mods)
        {
            return connection.ModifyAsync(distinguishedName, mods, CancellationToken.None);
        }

        public Task RenameAsync(string distinguishedName, string newRdn, bool deleteOldRdn)
        {
            return connection.RenameAsync(distinguishedName, newRdn, deleteOldRdn, CancellationToken.None);
        }

        /// <summary>
        /// Closes the connection once, also when called by the cancellation of its token.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                // Unregister instead of Dispose: it does not wait for the callback, which may be this very call
                cancellationRegistration.Unregister();
                connection.Dispose();
            }
        }
    }
}
