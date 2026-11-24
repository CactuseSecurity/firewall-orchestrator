namespace FWO.Basics.Enums
{
    /// <summary>
    /// Priority of an outgoing email, shown by mail clients as a flag or marker.
    /// Normal writes no priority headers; Low and High set the Importance, Priority and X-Priority headers.
    /// </summary>
    public enum EmailPriority
    {
        Normal = 0,
        Low = 1,
        High = 2
    }

    /// <summary>
    /// Display helpers for <see cref="EmailPriority"/>.
    /// </summary>
    public static class EmailPriorityExtensions
    {
        private const string kTextKeyPrefix = "email_priority_";

        private static readonly List<EmailPriority> kOfferedOptions = [EmailPriority.Low, EmailPriority.Normal, EmailPriority.High];

        /// <summary>
        /// Returns the priorities offered for selection, ordered from low to high.
        /// </summary>
        /// <returns>A new list of the offered priorities.</returns>
        public static List<EmailPriority> OfferedOptions()
        {
            return [.. kOfferedOptions];
        }

        /// <summary>
        /// Returns the key of the localized display text of the priority.
        /// </summary>
        /// <param name="priority">The priority to display.</param>
        /// <returns>The text key, e.g. "email_priority_high".</returns>
        public static string TextKey(this EmailPriority priority)
        {
            return kTextKeyPrefix + priority.ToString().ToLowerInvariant();
        }
    }
}
