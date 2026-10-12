namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the predicates supported by the create-ticket request contract.
/// </summary>
public static class CreateTicketPredicates
{
    /// <summary>Identifies creation of a new workflow entity.</summary>
    public const string kCreate = "create";

    /// <summary>Identifies modification of an existing workflow entity.</summary>
    public const string kModify = "modify";

    /// <summary>Identifies deletion of an existing workflow entity.</summary>
    public const string kDelete = "delete";
}
