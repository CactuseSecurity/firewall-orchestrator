namespace FWO.Api.Data
{
    public enum ExtStates
    {
        Done = 0,
        Rejected = 1,

        ExtReqInitialized = 10,
        ExtReqFailed = 20,
		ExtReqRequested = 21,
		ExtReqInProgress = 22,
		ExtReqRejected = 23,
		ExtReqDone = 24,
        ExtReqAckRejected = 30,
		ExtReqAcknowledged = 31
    }
}
