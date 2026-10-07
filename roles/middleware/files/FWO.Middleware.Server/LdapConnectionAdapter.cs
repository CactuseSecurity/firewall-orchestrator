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
        /// connection is closed when the token is cancelled, which ends the waiting operations: otherwise an abandoned
        /// login would hold its LDAP slot until the directory answers (see LdapAuthenticationGate).
        /// </remarks>
        /// <param name="connection">The connected Novell connection.</param>
        /// <param name="cancellationToken">Closes the connection when cancelled; default for connections without a deadline.</param>
        internal NovellLdapConnectionAdapter(LdapConnection connection, CancellationToken cancellationToken = default)
        {
            this.connection = connection;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(Dispose);
            }
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
