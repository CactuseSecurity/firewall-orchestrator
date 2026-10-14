namespace FWO.Middleware.Server
{
    /// <summary>
    /// Outcome of deleting the directory entry of a local user.
    /// </summary>
    public enum LdapUserDeletion
    {
        /// <summary>The directory entry was deleted.</summary>
        Deleted,
        /// <summary>
        /// The dn no longer names the directory object the local user is bound to - it is gone or now belongs to another
        /// account - so the directory entry was left untouched and only the local user may be deleted.
        /// </summary>
        NotBound,
        /// <summary>The directory could not be read or the entry could not be deleted.</summary>
        Failed
    }
}
