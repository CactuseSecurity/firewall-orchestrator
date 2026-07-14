namespace FWO.Data
{
    public enum EmailRecipientOption
    {
        None = 0,

        CurrentHandler = 1,
        RecentHandler = 2,
        AssignedGroup = 3,

        OwnerMainResponsible = 10,
        AllOwnerResponsibles = 11,
        OwnerGroupOnly = 12,
        FallbackToMainResponsibleIfOwnerGroupEmpty = 13,
        ConfiguredResponsibles = 14,

        Requester = 20,
        Approver = 21,
        LastCommenter = 30,
        // AllCommenters = 31

        OtherAddresses = 40
    }

    public static class EmailRecipientGroups
    {
        /// <summary>
        /// Returns recipient options that resolve against owner/modelling responsibility data.
        /// </summary>
        /// <returns>Available modelling recipient options.</returns>
        public static List<EmailRecipientOption> GetModellingOptions()
        {
            return [ EmailRecipientOption.None,
                EmailRecipientOption.ConfiguredResponsibles,
                EmailRecipientOption.OwnerGroupOnly,
                EmailRecipientOption.AllOwnerResponsibles,
                EmailRecipientOption.OwnerMainResponsible,
                EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty,
                EmailRecipientOption.OtherAddresses ];
        }

        /// <summary>
        /// Returns recipient options that resolve against workflow ticket context.
        /// </summary>
        /// <returns>Available workflow recipient options.</returns>
        public static List<EmailRecipientOption> GetWorkflowOptions()
        {
            return [
                EmailRecipientOption.None,
                EmailRecipientOption.CurrentHandler,
                EmailRecipientOption.RecentHandler,
                EmailRecipientOption.AssignedGroup,
                EmailRecipientOption.Requester,
                EmailRecipientOption.Approver,
                EmailRecipientOption.LastCommenter,
                EmailRecipientOption.OtherAddresses
            ];
        }

        /// <summary>
        /// Returns recipient options that do not need owner or workflow context.
        /// </summary>
        /// <returns>Available direct-address recipient options.</returns>
        public static List<EmailRecipientOption> GetDirectAddressOptions()
        {
            return [EmailRecipientOption.None, EmailRecipientOption.OtherAddresses];
        }
    }
}
