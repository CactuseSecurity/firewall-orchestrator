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
        /// <summary>
        /// Reads a single LDAP entry by distinguished name.
        /// </summary>
        Task<LdapEntry?> ReadAsync(string distinguishedName);
        /// <summary>
        /// Executes an LDAP search.
        /// </summary>
        Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly);
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

        internal NovellLdapConnectionAdapter(LdapConnection connection)
        {
            this.connection = connection;
        }

        public bool Bound => connection.Bound;

        public LdapConstraints SearchConstraints => connection.SearchConstraints;

        public LdapConstraints Constraints
        {
            get => (LdapConstraints)connection.Constraints;
            set => connection.Constraints = value;
        }

        public Task BindAsync(string user, string password)
        {
            return connection.BindAsync(user, password);
        }

        public Task<LdapEntry?> ReadAsync(string distinguishedName)
        {
            return connection.ReadAsync(distinguishedName);
        }

        public async Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly)
        {
            return await connection.SearchAsync(baseDn, scope, filter, attributes, typesOnly);
        }

        public Task AddAsync(LdapEntry entry)
        {
            return connection.AddAsync(entry);
        }

        public Task DeleteAsync(string distinguishedName)
        {
            return connection.DeleteAsync(distinguishedName);
        }

        public Task ModifyAsync(string distinguishedName, LdapModification[] mods)
        {
            return connection.ModifyAsync(distinguishedName, mods);
        }

        public Task RenameAsync(string distinguishedName, string newRdn, bool deleteOldRdn)
        {
            return connection.RenameAsync(distinguishedName, newRdn, deleteOldRdn);
        }

        public void Dispose()
        {
            connection.Dispose();
        }
    }
}
