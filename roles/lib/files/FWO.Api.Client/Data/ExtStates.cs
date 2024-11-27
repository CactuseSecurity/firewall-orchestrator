namespace FWO.Api.Data
{
    public enum ExtStates
    {
        Done = 0,
        Rejected = 1,

    	// states used in ExternalRequestHandler, to be mapped in internal workflow states
        ExtReqInitialized = 10,
        ExtReqFailed = 20,
		ExtReqRequested = 21,
		ExtReqInProgress = 22,
		ExtReqRejected = 23,
		ExtReqDone = 24,

        // to be used in ExternalRequestHandler, not reflected in internal workflow states 
        ExtReqAckRejected = 30,
		ExtReqAcknowledged = 31,
        ExtReqDiscarded = 32
    }
}
