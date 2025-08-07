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

        Requester = 20,
        Approver = 21,
        LastCommenter = 30,
        // AllCommenters = 31

        OtherAddresses = 40
    }

    public static class EmailRecipientGroups
    {
        public static List<EmailRecipientOption> GetModellingOptions()
        {
            return [ EmailRecipientOption.OwnerGroupOnly,
                EmailRecipientOption.AllOwnerResponsibles,
                EmailRecipientOption.OwnerMainResponsible,
                EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty ];
        }
    }
}
