namespace FWO.Basics.Exceptions
{
    public class EnvironmentException : Exception
	{
	    public EnvironmentException(string message) : base(message) {}
	    public EnvironmentException(string message, Exception innerException) : base(message, innerException) {}
	}
}
