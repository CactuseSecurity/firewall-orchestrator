namespace FWO.Basics.Exceptions
{
    public class ProcessingFailedException : Exception
	{
	    public ProcessingFailedException(string message) : base(message) {}
	    public ProcessingFailedException(string message, Exception innerException) : base(message, innerException) {}
	}
}
