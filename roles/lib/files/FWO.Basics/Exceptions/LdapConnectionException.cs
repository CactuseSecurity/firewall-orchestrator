namespace FWO.Basics.Exceptions
{
    public class LdapConnectionException : Exception
	{
	    public LdapConnectionException(string message) : base(message) {}
	    public LdapConnectionException(string message, Exception innerException) : base(message, innerException) {}
	}
}
