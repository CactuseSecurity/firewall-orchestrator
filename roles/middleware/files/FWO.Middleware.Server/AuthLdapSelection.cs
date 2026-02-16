namespace FWO.Middleware.Server
{
    /// <summary>
    /// Helper for deterministic LDAP login source selection.
    /// </summary>
    public static class AuthLdapSelection
    {
        /// <summary>
        /// Selects the first successful LDAP index based on configured order.
        /// </summary>
        /// <param name="loginSuccessByOrder">Login results in LDAP configuration order.</param>
        /// <returns>Index of preferred LDAP or -1 if none succeeded.</returns>
        public static int GetPreferredLdapIndex(IReadOnlyList<bool>? loginSuccessByOrder)
        {
            if (loginSuccessByOrder == null || loginSuccessByOrder.Count == 0)
            {
                return -1;
            }

            for (int index = 0; index < loginSuccessByOrder.Count; index++)
            {
                if (loginSuccessByOrder[index])
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
