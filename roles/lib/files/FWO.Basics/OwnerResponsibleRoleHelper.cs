using System.Collections.Generic;
using System.Linq;

namespace FWO.Basics
{
    public static class OwnerResponsibleRoleHelper
    {
        public static bool IsModellingRole(string role)
        {
            return role == Roles.Modeller;
        }

        public static bool IsRecertificationRole(string role)
        {
            return role == Roles.Recertifier;
        }

        public static bool IsWriteSensitiveRole(string role)
        {
            return IsModellingRole(role) || IsRecertificationRole(role);
        }

        public static List<string> FilterRoles(IEnumerable<string> roles, bool allowModelling, bool allowRecertification)
        {
            List<string> roleList = roles.ToList();
            if (allowModelling && allowRecertification)
            {
                return roleList;
            }

            return roleList
                .Where(role =>
                    (!IsModellingRole(role) || allowModelling)
                    && (!IsRecertificationRole(role) || allowRecertification))
                .ToList();
        }
    }
}
