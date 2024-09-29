namespace FWO.Api.Data
{
    public enum ExtStates
    {
        Done = 0,
        Rejected = 1,

        ExtReqInitialized = 10,
        ExtReqFailed = 11,
		ExtReqRequested = 12,
		ExtReqInProgress = 13,
		ExtReqRejected = 14,
		ExtReqDone = 15,
		ExtReqAcknowledged = 16
    }
}
