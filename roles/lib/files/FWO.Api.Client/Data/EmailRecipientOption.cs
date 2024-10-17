namespace FWO.Api.Data
{
    public enum EmailRecipientOption
    {
        CurrentHandler = 1,
        RecentHandler = 2,
        AssignedGroup = 3,
        
        OwnerMainResponsible = 10, 
        AllOwnerResponsibles = 11,
        OwnerGroupOnly = 12,
        FallbackToMainResponsibleIfOwnerGroupEmpty = 13,

        Requester = 20,
        Approver = 21,
        LastCommenter = 30
        // AllCommenters = 31
    }
}
