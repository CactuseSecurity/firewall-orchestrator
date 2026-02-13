using FWO.Data;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Selects the default owner in recertification context.
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
    }
}
