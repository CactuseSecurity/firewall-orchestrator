using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using RestSharp;
using System.Net;
using System.Security.Authentication;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Vendor-independent category of a failed firewall write. It tells the caller whether trying the task again later can
    /// help (<see cref="FirewallWriteError.IsRetryable"/>) or the task fails until someone changes it, its target or the settings.
    /// </summary>
    public enum FirewallWriteErrorKind
    {
        /// <summary>Endpoint, template, device specific settings, management data or certificate trust are wrong.</summary>
        Configuration,
        /// <summary>The implementation task lacks data needed for the write, e.g. rule elements, an action or object names.</summary>
        InvalidTaskData,
        /// <summary>The device type, task type or operation cannot be written.</summary>
        Unsupported,
        /// <summary>The firewall refused the credentials or the session.</summary>
        Authentication,
        /// <summary>An object, group or rule the call refers to does not exist on the firewall.</summary>
        NotFound,
        /// <summary>An object to create already exists on the firewall.</summary>
        AlreadyExists,
        /// <summary>The firewall rejected the content of the call, e.g. a validation error.</summary>
        Rejected,
        /// <summary>The firewall or the network in between did not answer: connection, DNS, timeout or gateway errors.</summary>
        Unreachable,
        /// <summary>The firewall asked to slow down.</summary>
        RateLimited,
        /// <summary>A concurrent change got in the way: locked object or session, or a changed ETag.</summary>
        Conflict,
        /// <summary>The firewall reported an internal error.</summary>
        FirewallError,
        /// <summary>The failure could not be classified.</summary>
        Unknown
    }

    /// <summary>
    /// A failed firewall write: the normalized <see cref="Kind"/> and what the firewall (or the library) said about it.
    /// </summary>
    /// <param name="Kind">Vendor-independent category.</param>
    /// <param name="NativeMessage">The firewall's own error message (response body, shortened), or the library's message.</param>
    /// <param name="StatusCode">HTTP status of the failed call; null if no call was answered.</param>
    /// <param name="NativeCode">The vendor's own error code, e.g. "generic_err_object_not_found", "-3" or "AuthorizationFailed".</param>
    /// <param name="Operation">The operation that failed; null if the failure happened outside a call, e.g. at login.</param>
    public sealed record FirewallWriteError(FirewallWriteErrorKind Kind, string NativeMessage, int? StatusCode = null, string? NativeCode = null,
        FirewallWriteOperation? Operation = null)
    {
        /// <summary>
        /// Whether trying the task again later may succeed. Like the external request handling, unclassified failures are
        /// retried, because the number of attempts is limited anyway.
        /// </summary>
        public bool IsRetryable => FirewallWriteErrors.IsRetryable(Kind);
    }

    /// <summary>
    /// A firewall write failure with its normalized error, thrown inside the library and turned into the result. The
    /// exception message describes the failure, the error keeps the firewall's native message.
    /// </summary>
    public sealed class FirewallWriteException : Exception
    {
        public FirewallWriteError Error { get; }

        /// <summary>Creates an exception for a failure without firewall answer, e.g. a configuration or task data error.</summary>
        public FirewallWriteException(FirewallWriteErrorKind kind, string message, Exception? innerException = null)
            : this(new FirewallWriteError(kind, message), message, innerException)
        { }

        /// <summary>Creates an exception for a classified failure of a firewall call.</summary>
        public FirewallWriteException(FirewallWriteError error, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Error = error;
        }
    }

    /// <summary>The vendor-independent part of the error classification: retry decision, HTTP status and exceptions.</summary>
    public static class FirewallWriteErrors
    {
        private const int kMaxNativeMessageLength = 2000;
        private const int kFirstServerError = 500;
        private const int kFirstClientError = 400;
        private const HttpStatusCode kLocked = (HttpStatusCode)423;

        private static readonly HashSet<FirewallWriteErrorKind> RetryableKinds =
        [
            FirewallWriteErrorKind.Unreachable, FirewallWriteErrorKind.RateLimited, FirewallWriteErrorKind.Conflict,
            FirewallWriteErrorKind.FirewallError, FirewallWriteErrorKind.Unknown
        ];

        /// <summary>Whether a failure of the given kind may go away by itself, so the task can be tried again later.</summary>
        public static bool IsRetryable(FirewallWriteErrorKind kind) => RetryableKinds.Contains(kind);

        /// <summary>Category of an HTTP status, for vendors without a more specific error code.</summary>
        public static FirewallWriteErrorKind FromStatusCode(HttpStatusCode statusCode) => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FirewallWriteErrorKind.Authentication,
            HttpStatusCode.NotFound => FirewallWriteErrorKind.NotFound,
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or kLocked => FirewallWriteErrorKind.Conflict,
            HttpStatusCode.TooManyRequests => FirewallWriteErrorKind.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout => FirewallWriteErrorKind.Unreachable,
            _ when (int)statusCode >= kFirstServerError => FirewallWriteErrorKind.FirewallError,
            _ when (int)statusCode >= kFirstClientError => FirewallWriteErrorKind.Rejected,
            _ => FirewallWriteErrorKind.Unknown
        };

        /// <summary>
        /// Category of a response that was not answered by the firewall: a timeout or a connection failure is
        /// <see cref="FirewallWriteErrorKind.Unreachable"/>, a refused server certificate a configuration error, because a
        /// retry cannot fix it. Null if the firewall answered.
        /// </summary>
        public static FirewallWriteErrorKind? FromTransport(RestResponse response)
        {
            if (response.ResponseStatus == ResponseStatus.TimedOut)
            {
                return FirewallWriteErrorKind.Unreachable;
            }
            if (response.StatusCode != 0)
            {
                return null;
            }
            return HasInner<AuthenticationException>(response.ErrorException) ? FirewallWriteErrorKind.Configuration : FirewallWriteErrorKind.Unreachable;
        }

        /// <summary>Classifies an exception raised outside a firewall answer, e.g. while loading the task's target.</summary>
        public static FirewallWriteError FromException(Exception exception)
        {
            if (exception is FirewallWriteException known)
            {
                return known.Error;
            }
            FirewallWriteErrorKind kind = exception switch
            {
                ConfigException => FirewallWriteErrorKind.Configuration,
                _ when HasInner<AuthenticationException>(exception) => FirewallWriteErrorKind.Configuration,
                HttpRequestException or TimeoutException => FirewallWriteErrorKind.Unreachable,
                TaskCanceledException canceled when canceled.InnerException is TimeoutException => FirewallWriteErrorKind.Unreachable,
                _ => FirewallWriteErrorKind.Unknown
            };
            return new FirewallWriteError(kind, exception.Message);
        }

        /// <summary>The native message of a response: its body, else the transport error, shortened for logs and results.</summary>
        public static string NativeMessage(RestResponse response)
        {
            string content = string.IsNullOrWhiteSpace(response.Content)
                ? response.ErrorMessage ?? response.StatusDescription ?? ""
                : response.Content;
            return content.Length > kMaxNativeMessageLength ? content[..kMaxNativeMessageLength] : content;
        }

        private static bool HasInner<TException>(Exception? exception) where TException : Exception
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TException)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
