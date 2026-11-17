namespace FWO.Data.Modelling
{
    /// <summary>
    /// A connection the variance analysis leaves out, together with the reason.
    /// </summary>
    public class ModellingExcludedConnection
    {
        /// <summary>
        /// The connection that is not analysed.
        /// </summary>
        public ModellingConnection Connection { get; set; } = new();

        /// <summary>
        /// Why the connection is not analysed.
        /// </summary>
        public ConState Reason { get; set; }

        /// <summary>
        /// Gets the text key describing the reason, as shown in the connection table.
        /// </summary>
        public string ReasonTextKey => Reason switch
        {
            ConState.InterfaceRequested => "C9007",
            ConState.InterfaceRejected => "C9011",
            ConState.InterfaceDecommissioned => "C9026",
            ConState.InterfaceNoPermission => "C9028",
            ConState.EmptyAppRoles => "C9013",
            ConState.DeletedObjects => "C9014",
            ConState.EmptySvcGrps => "C9018",
            _ => Reason.ToString()
        };
    }
}
