using FWO.Data.Workflow;
using FWO.Data.Flow;

namespace FWO.Ui.Shared
{
    /// <summary>Stores the selected time object and date visibility state of the target-date editor.</summary>
    public sealed class TargetDatesEditorModel
    {
        /// <summary>Gets or sets the validity start.</summary>
        public DateTime? BeginDate { get; set; }
        /// <summary>Gets or sets the validity end.</summary>
        public DateTime? EndDate { get; set; }
        /// <summary>Gets or sets the selected Flow DB time-object ID.</summary>
        public long? FlowTimeObjectId { get; set; }
        /// <summary>Gets or sets the selected ticket-local element ID.</summary>
        public long? TicketElementId { get; set; }
        /// <summary>Gets or sets the selected time-object name.</summary>
        public string? FlowTimeObjectName { get; set; }
        /// <summary>Gets the ticket-local time objects offered by the selector.</summary>
        public List<FlowTimeObject> TicketTimeObjects { get; set; } = [];
        /// <summary>Gets the selector-to-element mapping for ticket-local objects.</summary>
        public Dictionary<long, long> TicketTimeObjectElementIds { get; set; } = [];
        /// <summary>Gets or sets whether the validity start is visible.</summary>
        public bool ShowBeginDate { get; set; } = true;
        /// <summary>Gets or sets whether the validity end is visible.</summary>
        public bool ShowEndDate { get; set; } = true;

        /// <summary>Initializes an empty target-date editor model.</summary>
        public TargetDatesEditorModel()
        { }

        /// <summary>Creates a copy of an existing target-date editor model.</summary>
        /// <param name="other">The model to copy.</param>
        public TargetDatesEditorModel(TargetDatesEditorModel other)
        {
            BeginDate = other.BeginDate;
            EndDate = other.EndDate;
            FlowTimeObjectId = other.FlowTimeObjectId;
            TicketElementId = other.TicketElementId;
            FlowTimeObjectName = other.FlowTimeObjectName;
            TicketTimeObjects = [.. other.TicketTimeObjects];
            TicketTimeObjectElementIds = new(other.TicketTimeObjectElementIds);
            ShowBeginDate = other.ShowBeginDate;
            ShowEndDate = other.ShowEndDate;
        }
    }
}
