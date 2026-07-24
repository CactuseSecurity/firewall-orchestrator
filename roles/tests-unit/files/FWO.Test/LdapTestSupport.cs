using System;
using Novell.Directory.Ldap;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FWO.Encryption;
using FWO.Middleware.Server;

namespace FWO.Test
{
    internal static class LdapTestSupport
    {
        private const string kTestMainKey = "0123456789ABCDEF0123456789ABCDEF";

        public static string CreateEncryptedSecret(string secret)
        {
            return AesEnc.Encrypt(secret, kTestMainKey);
        }

        public static LdapEntry CreateEntry(string dn, params LdapAttribute[] attributes)
        {
            LdapAttributeSet attributeSet = new();
            foreach (LdapAttribute attribute in attributes)
            {
                attributeSet.Add(attribute);
            }
            return new LdapEntry(dn, attributeSet);
        }

        public static FakeSearchResults CreateSearchResults(params LdapEntry[] entries)
        {
            return new FakeSearchResults(entries);
        }
    }

    internal sealed class TestableLdap : FWO.Middleware.Server.Ldap
    {
        private readonly ILdapClient connection;

        public TestableLdap(ILdapClient connection)
        {
            this.connection = connection;
        }

        protected override Task<ILdapClient> Connect()
        {
            return Task.FromResult(connection);
        }
    }

    internal sealed class RecordingLdapClient : ILdapClient
    {
        public bool Bound { get; private set; }
        public LdapConstraints SearchConstraints { get; } = new();
        public LdapConstraints Constraints { get; set; } = new();
        public LdapEntry? ReadResult { get; set; }
        public ILdapSearchResults? SearchResults { get; set; }
        public Func<string?, int, string, string[]?, bool, ILdapSearchResults?>? SearchResponder { get; set; }
        public List<string> BoundUsers { get; } = new();
        public List<(string User, string Password)> BindCalls { get; } = new();
        public List<string> ReadCalls { get; } = new();
        public List<(string? BaseDn, int Scope, string Filter, string[]? Attributes, bool TypesOnly)> SearchCalls { get; } = new();
        public List<LdapEntry> AddedEntries { get; } = new();
        public List<string> DeletedDns { get; } = new();
        public List<(string DistinguishedName, LdapModification[] Mods)> ModifyCalls { get; } = new();
        public List<(string DistinguishedName, string NewRdn, bool DeleteOldRdn)> RenameCalls { get; } = new();
        public Dictionary<string, LdapEntry?> ReadResultsByDn { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool ThrowOnAdd { get; set; }
        public bool ThrowOnDelete { get; set; }

        public Task BindAsync(string user, string password)
        {
            BindCalls.Add((user, password));
            BoundUsers.Add(user);
            Bound = true;
            return Task.CompletedTask;
        }

        public Task<LdapEntry?> ReadAsync(string distinguishedName)
        {
            ReadCalls.Add(distinguishedName);
            if (ReadResultsByDn.TryGetValue(distinguishedName, out LdapEntry? result))
            {
                return Task.FromResult(result);
            }
            return Task.FromResult(ReadResult);
        }

        public Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly)
        {
            SearchCalls.Add((baseDn, scope, filter, attributes, typesOnly));
            if (SearchResponder != null)
            {
                return Task.FromResult(SearchResponder(baseDn, scope, filter, attributes, typesOnly));
            }
            return Task.FromResult(SearchResults);
        }

        public Task AddAsync(LdapEntry entry)
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("add failed");
            }
            AddedEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string distinguishedName)
        {
            if (ThrowOnDelete)
            {
                throw new InvalidOperationException("delete failed");
            }
            DeletedDns.Add(distinguishedName);
            return Task.CompletedTask;
        }

        public Task ModifyAsync(string distinguishedName, LdapModification[] mods)
        {
            ModifyCalls.Add((distinguishedName, mods));
            return Task.CompletedTask;
        }

        public Task RenameAsync(string distinguishedName, string newRdn, bool deleteOldRdn)
        {
            RenameCalls.Add((distinguishedName, newRdn, deleteOldRdn));
            return Task.CompletedTask;
        }

        public void Dispose()
        { }
    }

    internal sealed class FakeSearchResults : ILdapSearchResults
    {
        private readonly List<LdapEntry> entries;
        private int index;

        public FakeSearchResults(IEnumerable<LdapEntry> entries)
        {
            this.entries = entries.ToList();
        }

        public LdapControl[] ResponseControls { get; private set; } = Array.Empty<LdapControl>();

        public Task<bool> HasMoreAsync(CancellationToken ct = default)
        {
            return Task.FromResult(index < entries.Count);
        }

        public Task<LdapEntry> NextAsync(CancellationToken ct = default)
        {
            if (index >= entries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(ct));
            }

            return Task.FromResult(entries[index++]);
        }

        public IAsyncEnumerator<LdapEntry> GetAsyncEnumerator(CancellationToken ct = default)
        {
            return new FakeSearchResultsEnumerator(this, ct);
        }

        private sealed class FakeSearchResultsEnumerator : IAsyncEnumerator<LdapEntry>
        {
            private readonly FakeSearchResults parent;
            private readonly CancellationToken cancellationToken;

            public FakeSearchResultsEnumerator(FakeSearchResults parent, CancellationToken cancellationToken)
            {
                this.parent = parent;
                this.cancellationToken = cancellationToken;
            }

            public LdapEntry Current { get; private set; } = null!;

            public async ValueTask<bool> MoveNextAsync()
            {
                if (!await parent.HasMoreAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                Current = await parent.NextAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
