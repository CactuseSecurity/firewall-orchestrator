namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Thrown when a service's first API query keeps failing for its whole startup budget.
    /// </summary>
    /// <remarks>
    /// Distinct from a plain connection failure because it ends the startup rather than a
    /// single request: the message it carries is the only thing an operator will see, so it
    /// is written for that audience and must name the endpoint that was addressed.
    /// </remarks>
    public class ApiUnavailableAtStartupException : Exception
    {
        public ApiUnavailableAtStartupException(string message, Exception? innerException) : base(message, innerException) { }
    }
}
