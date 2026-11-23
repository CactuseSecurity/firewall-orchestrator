using System.Text.Json;

namespace FWO.Data.Ai
{
    /// <summary>
    /// Role filter of an AI function (<see cref="AiSettings.ChatRoles"/>, <see cref="AiSettings.ReasonCheckRoles"/>):
    /// the roles allowed to use the function, stored in the global config as JSON array of role names. An empty list
    /// restricts nothing, so that every user may use the function while it is switched on. The UI hides the function
    /// from other users and the AI harness refuses their runs, checking the roles of the user's JWT.
    /// </summary>
    public static class AiFunctionRoles
    {
        /// <summary>Stored value of a role filter that restricts nothing.</summary>
        public const string kNoRestriction = "[]";

        /// <summary>
        /// Reads a stored role filter: role names trimmed, without blank names and duplicates (ignoring case), sorted.
        /// A blank text is an empty filter, as the setting was not stored yet.
        /// </summary>
        /// <param name="text">Stored value, a JSON array of role names</param>
        /// <param name="roles">The roles of the filter; empty if the value cannot be read</param>
        /// <returns>False if the value is not a JSON array of role names. The caller has to treat the function as
        /// switched off then, so that an unreadable filter does not allow every user.</returns>
        public static bool TryParse(string? text, out List<string> roles)
        {
            roles = [];
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            try
            {
                List<string?>? storedRoles = JsonSerializer.Deserialize<List<string?>>(text);
                if (storedRoles == null)
                {
                    return false;
                }
                roles = Normalize(storedRoles);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the value to store for the given roles, normalized like <see cref="TryParse"/> does, so that the same
        /// roles always give the same value.
        /// </summary>
        /// <param name="roles">Roles allowed to use the function; none restricts nothing</param>
        public static string Serialize(IEnumerable<string?> roles)
        {
            ArgumentNullException.ThrowIfNull(roles);
            return JsonSerializer.Serialize(Normalize(roles));
        }

        /// <summary>
        /// Tells whether a user holding the given roles may use a function with the given role filter: always for an
        /// empty filter, otherwise if the user holds at least one of its roles (ignoring case).
        /// </summary>
        /// <param name="allowedRoles">Role filter of the function</param>
        /// <param name="userRoles">Roles of the user</param>
        public static bool IsAllowed(IReadOnlyCollection<string> allowedRoles, IEnumerable<string> userRoles)
        {
            ArgumentNullException.ThrowIfNull(allowedRoles);
            ArgumentNullException.ThrowIfNull(userRoles);
            if (allowedRoles.Count == 0)
            {
                return true;
            }
            return userRoles.Any(userRole => allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the roles trimmed, without blank names and duplicates (ignoring case), sorted.
        /// </summary>
        private static List<string> Normalize(IEnumerable<string?> roles)
        {
            IEnumerable<string> trimmedRoles = roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!.Trim());
            return [.. trimmedRoles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(role => role, StringComparer.OrdinalIgnoreCase)];
        }
    }
}
