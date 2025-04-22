using FWO.Data.Modelling;

namespace FWO.Services
{
    public class AppRoleComparer() : IEqualityComparer<ModellingAppRole?>
    {
        public bool Equals(ModellingAppRole? appRole1, ModellingAppRole? appRole2)
        {
            if (ReferenceEquals(appRole1, appRole2))
            {
                return true;
            }

            if (appRole1 is null || appRole2 is null)
            {
                return false;
            }

            return (appRole1.Name == appRole2.Name);
        }

        public int GetHashCode(ModellingAppRole appRole)
        {
            return HashCode.Combine(appRole.Name);
        }
    }
}
