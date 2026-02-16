using FWO.Data;
using FWO.Ui.Auth;
using System.Security.Claims;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Provides reusable recertification owner selection and access rules.
    /// </summary>
    public static class RecertificationOwnerSelection
    {
        /// <summary>
        /// Picks a default owner and prioritizes recertifiable owners when available.
        /// </summary>
        public static FwoOwner? SelectOwner(IReadOnlyList<FwoOwner> owners, IReadOnlyCollection<int> recertifiableOwnerIds)
        {
            if (owners.Count == 0)
            {
                return null;
            }

            if (recertifiableOwnerIds.Count == 0)
            {
                return owners[0];
            }

            return owners.FirstOrDefault(owner => recertifiableOwnerIds.Contains(owner.Id)) ?? owners[0];
        }

        /// <summary>
        /// Resolves owner ids from configured values and falls back to JWT claim values when empty.
        /// </summary>
        public static List<int> ResolveOwnerIds(IReadOnlyList<int> configuredOwnerIds, IEnumerable<Claim> claims, string claimType)
        {
            if (configuredOwnerIds.Count > 0)
            {
                return [.. configuredOwnerIds];
            }

            return JwtClaimParser.ExtractIntClaimValues(claims, claimType);
        }

        /// <summary>
        /// Determines whether the selected owner can be updated in recertification context.
        /// </summary>
        public static bool CanWriteSelectedOwner(
            FwoOwner? selectedOwner,
            bool isAdmin,
            bool hasRecertifierRole,
            IReadOnlyCollection<int> recertifiableOwnerIds)
        {
            if (selectedOwner == null || (!isAdmin && !hasRecertifierRole))
            {
                return false;
            }

            if (isAdmin)
            {
                return true;
            }

            return recertifiableOwnerIds.Contains(selectedOwner.Id);
        }
    }
}
