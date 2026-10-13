using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class LdapDnNamespaceTest
    {
        private const string kInternalUserPath = "ou=operator,ou=user,dc=fworch,dc=internal";
        private const string kInternalRolePath = "ou=role,dc=fworch,dc=internal";
        private const string kInternalGroupPath = "ou=group,dc=fworch,dc=internal";
        private const string kExternalUserPath = "ou=users,dc=example,dc=com";
        private const string kExternalGroupPath = "ou=groups,dc=example,dc=com";
        private const string kInternalUserDn = "uid=x,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
        private const int kInternalId = 1;
        private const int kExternalId = 2;
        private static readonly List<string> kEscapedCommaRdns = ["cn=doe, john", "ou=users", "dc=example", "dc=com"];
        private static readonly List<int> kInternalIdOnly = [kInternalId];
        private static readonly List<int> kBothIds = [kInternalId, kExternalId];

        [Test]
        public void SplitDn_NormalizesCaseAndSpacesAndKeepsEscapedSeparators()
        {
            List<string> rdns = LdapDnNamespace.SplitDn(" CN = Doe\\, John , ou=Users,DC=example; dc=com ");

            Assert.That(rdns, Is.EqualTo(kEscapedCommaRdns));
        }

        [Test]
        public void SplitDn_ReturnsEmptyForEmptyDn()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LdapDnNamespace.SplitDn(null), Is.Empty);
                Assert.That(LdapDnNamespace.SplitDn("  "), Is.Empty);
            });
        }

        [Test]
        public void GetNamespaceRoots_SkipsEmptyAndDuplicatePaths()
        {
            LdapConnectionBase ldap = new()
            {
                UserSearchPath = kInternalUserPath,
                RoleSearchPath = "",
                GroupSearchPath = kInternalGroupPath,
                GroupWritePath = "OU=group, dc=fworch,dc=internal"
            };

            Assert.That(LdapDnNamespace.GetNamespaceRoots(ldap), Has.Count.EqualTo(2));
        }

        [Test]
        public void GetClaimDepth_UsesTheMostSpecificContainingPath()
        {
            LdapConnectionBase ldap = CreateInternal();

            Assert.Multiple(() =>
            {
                Assert.That(LdapDnNamespace.GetClaimDepth(ldap, kInternalUserDn), Is.EqualTo(4));
                Assert.That(LdapDnNamespace.GetClaimDepth(ldap, "cn=admin,ou=role,dc=fworch,dc=internal"), Is.EqualTo(3));
                Assert.That(LdapDnNamespace.GetClaimDepth(ldap, "uid=x,ou=users,dc=example,dc=com"), Is.Zero);
                // a path is no rdn-wise suffix of a dn that merely ends with the same characters
                Assert.That(LdapDnNamespace.GetClaimDepth(ldap, "uid=x,ou=xoperator,ou=user,dc=fworch,dc=internal"), Is.Zero);
                Assert.That(LdapDnNamespace.GetClaimDepth(ldap, ""), Is.Zero);
            });
        }

        [Test]
        public void IsOwnedBy_AcceptsOnlyTheConnectionWhosePathsContainTheDn()
        {
            List<LdapConnectionBase> ldaps = [CreateInternal(), CreateExternal()];

            Assert.Multiple(() =>
            {
                Assert.That(LdapDnNamespace.IsOwnedBy(ldaps, kInternalId, kInternalUserDn), Is.True);
                Assert.That(LdapDnNamespace.IsOwnedBy(ldaps, kExternalId, kInternalUserDn), Is.False);
                Assert.That(LdapDnNamespace.IsOwnedBy(ldaps, kExternalId, "UID=x, OU=users,dc=example,dc=com"), Is.True);
                Assert.That(LdapDnNamespace.IsOwnedBy(ldaps, kInternalId, "uid=x,ou=users,dc=example,dc=com"), Is.False);
            });
        }

        [Test]
        public void GetClaimingLdapIds_PrefersTheMoreSpecificPath()
        {
            LdapConnectionBase nested = CreateExternal();
            nested.UserSearchPath = "ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
            nested.GroupSearchPath = "";
            List<LdapConnectionBase> ldaps = [CreateInternal(), nested];

            Assert.Multiple(() =>
            {
                Assert.That(LdapDnNamespace.GetClaimingLdapIds(ldaps, kInternalUserDn), Is.EqualTo(new List<int> { kExternalId }));
                Assert.That(LdapDnNamespace.GetClaimingLdapIds(ldaps, "uid=y,ou=tenant1,ou=operator,ou=user,dc=fworch,dc=internal"), Is.EqualTo(kInternalIdOnly));
            });
        }

        [Test]
        public void GetClaimingLdapIds_ReportsAmbiguousAndIgnoresInactiveConnections()
        {
            LdapConnectionBase duplicate = CreateInternal();
            duplicate.Id = kExternalId;
            List<LdapConnectionBase> ldaps = [CreateInternal(), duplicate];

            List<int> ambiguous = LdapDnNamespace.GetClaimingLdapIds(ldaps, kInternalUserDn);
            bool ownedWhileAmbiguous = LdapDnNamespace.IsOwnedBy(ldaps, kInternalId, kInternalUserDn);
            duplicate.Active = false;
            List<int> afterDeactivation = LdapDnNamespace.GetClaimingLdapIds(ldaps, kInternalUserDn);

            Assert.Multiple(() =>
            {
                Assert.That(ambiguous, Is.EqualTo(kBothIds));
                Assert.That(ownedWhileAmbiguous, Is.False);
                Assert.That(afterDeactivation, Is.EqualTo(kInternalIdOnly));
            });
        }

        [Test]
        public void GetOverlappingLdaps_FindsEqualPathsButNotNestedOnes()
        {
            LdapConnectionBase internalLdap = CreateInternal();
            LdapConnectionBase equalPaths = CreateExternal();
            equalPaths.UserSearchPath = kInternalUserPath.ToUpperInvariant();
            LdapConnectionBase broaderPath = CreateExternal();
            broaderPath.Id = 3;
            broaderPath.UserSearchPath = "dc=internal";
            LdapConnectionBase disjoint = CreateExternal();
            disjoint.Id = 4;
            List<LdapConnectionBase> others = [equalPaths, broaderPath, disjoint];

            List<LdapConnectionBase> overlapping = LdapDnNamespace.GetOverlappingLdaps(internalLdap, others);

            // the broader path is no overlap: the more specific internal paths own their subtrees
            Assert.That(overlapping.Select(ldap => ldap.Id), Is.EqualTo(new List<int> { kExternalId }));
        }

        [Test]
        public void GetOverlappingLdaps_IgnoresTheConnectionItselfAndInactiveConnections()
        {
            LdapConnectionBase internalLdap = CreateInternal();
            LdapConnectionBase inactiveCopy = CreateInternal();
            inactiveCopy.Id = kExternalId;
            inactiveCopy.Active = false;
            List<LdapConnectionBase> others = [internalLdap, inactiveCopy];
            LdapConnectionBase inactiveCandidate = CreateInternal();
            inactiveCandidate.Id = 0;
            inactiveCandidate.Active = false;

            Assert.Multiple(() =>
            {
                Assert.That(LdapDnNamespace.GetOverlappingLdaps(internalLdap, others), Is.Empty);
                Assert.That(LdapDnNamespace.GetOverlappingLdaps(inactiveCandidate, others), Is.Empty);
            });
        }

        [Test]
        public void GetOverlappingLdaps_TreatsANewConnectionAsDifferentFromAllExistingOnes()
        {
            LdapConnectionBase newConnection = CreateInternal();
            newConnection.Id = 0;
            List<LdapConnectionBase> others = [CreateInternal()];

            Assert.That(LdapDnNamespace.GetOverlappingLdaps(newConnection, others), Has.Count.EqualTo(1));
        }

        [Test]
        public void WarnAboutOverlappingNamespaces_CountsEveryActiveOverlappingConnection()
        {
            List<Ldap> ldaps =
            [
                CreateMiddlewareLdap(kInternalId, kInternalUserPath),
                CreateMiddlewareLdap(kExternalId, kInternalUserPath),
                CreateMiddlewareLdap(3, kExternalUserPath)
            ];
            List<Ldap> disjointLdaps = [ldaps[0], ldaps[2]];

            Assert.Multiple(() =>
            {
                Assert.That(AuthDirectoryBinding.WarnAboutOverlappingNamespaces(ldaps), Is.EqualTo(2));
                Assert.That(AuthDirectoryBinding.WarnAboutOverlappingNamespaces(disjointLdaps), Is.Zero);
            });
        }

        private static TestableLdap CreateMiddlewareLdap(int id, string userSearchPath)
        {
            return new TestableLdap(new RecordingLdapClient())
            {
                Id = id,
                Addresses = [$"ldap{id}.example.test"],
                UserSearchPath = userSearchPath
            };
        }

        private static LdapConnectionBase CreateInternal()
        {
            return new LdapConnectionBase
            {
                Id = kInternalId,
                UserSearchPath = kInternalUserPath,
                RoleSearchPath = kInternalRolePath,
                GroupSearchPath = kInternalGroupPath,
                GroupWritePath = kInternalGroupPath
            };
        }

        private static LdapConnectionBase CreateExternal()
        {
            return new LdapConnectionBase
            {
                Id = kExternalId,
                UserSearchPath = kExternalUserPath,
                GroupSearchPath = kExternalGroupPath
            };
        }
    }
}
