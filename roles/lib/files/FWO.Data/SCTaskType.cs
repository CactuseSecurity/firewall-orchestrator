namespace FWO.Data
{
    /// <summary>
    /// Task types of Tufin SecureChange tickets. Their names are used as task type of the ticket templates and in the
    /// device specific setting keys of the SecureChange templates.
    /// </summary>
    public enum SCTaskType
    {
        AccessRequest = 0,
        NetworkObjectModify = 10,
        NetworkServiceCreate = 20,
        NetworkServiceUpdate = 21
    }
}
