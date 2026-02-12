using System.Security.Claims;
using System.Text.Json;

namespace FWO.Ui.Auth
{
    public static class JwtClaimParser
    {
        public static List<string> ExtractStringClaimValues(IEnumerable<Claim> claims, string claimType)
        {
            List<string> result = [];
            foreach (Claim claim in claims.Where(currentClaim => ClaimTypeMatches(currentClaim.Type, claimType)))
            {
                AddStringClaimValue(result, claim.Value);
            }
            return result;
        }

        public static List<int> ExtractIntClaimValues(IEnumerable<Claim> claims, string claimType)
        {
            List<int> result = [];
            foreach (Claim claim in claims.Where(currentClaim => ClaimTypeMatches(currentClaim.Type, claimType)))
            {
                AddIntClaimValue(result, claim.Value);
            }
            return result;
        }

        private static void AddStringClaimValue(List<string> result, string claimValue)
        {
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return;
            }

            string trimmedValue = claimValue.Trim();
            if (TryDeserializeStringArray(trimmedValue, out List<string> parsedStrings))
            {
                AddDistinctStrings(result, parsedStrings);
                return;
            }

            if (trimmedValue.StartsWith('{') && trimmedValue.EndsWith('}'))
            {
                AddDistinctStrings(result, SplitSeparatedValues(trimmedValue));
                return;
            }

            if (!result.Contains(trimmedValue, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(trimmedValue);
            }
        }

        private static void AddIntClaimValue(List<int> result, string claimValue)
        {
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return;
            }

            string trimmedValue = claimValue.Trim();
            if (TryDeserializeIntArray(trimmedValue, out List<int> parsedInts))
            {
                AddDistinctInts(result, parsedInts);
                return;
            }

            foreach (string token in SplitSeparatedValues(trimmedValue))
            {
                if (int.TryParse(token, out int parsedInt) && !result.Contains(parsedInt))
                {
                    result.Add(parsedInt);
                }
            }
        }

        private static bool TryDeserializeStringArray(string claimValue, out List<string> values)
        {
            values = [];
            try
            {
                string[]? directStringArray = JsonSerializer.Deserialize<string[]>(claimValue);
                if (directStringArray == null)
                {
                    return false;
                }
                values = directStringArray.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryDeserializeIntArray(string claimValue, out List<int> values)
        {
            values = [];
            try
            {
                int[]? directIntArray = JsonSerializer.Deserialize<int[]>(claimValue);
                if (directIntArray != null)
                {
                    values = directIntArray.ToList();
                    return true;
                }

                string[]? stringIntArray = JsonSerializer.Deserialize<string[]>(claimValue);
                if (stringIntArray == null)
                {
                    return false;
                }

                foreach (string value in stringIntArray)
                {
                    if (int.TryParse(value, out int parsedInt))
                    {
                        values.Add(parsedInt);
                    }
                }
                return values.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> SplitSeparatedValues(string value)
        {
            char[] separators = [',', '{', '}', '[', ']'];
            return value
                .Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToList();
        }

        private static void AddDistinctStrings(List<string> target, IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    target.Add(value);
                }
            }
        }

        private static void AddDistinctInts(List<int> target, IEnumerable<int> values)
        {
            foreach (int value in values)
            {
                if (!target.Contains(value))
                {
                    target.Add(value);
                }
            }
        }

        private static bool ClaimTypeMatches(string claimType, string expectedClaimType)
        {
            if (claimType.Equals(expectedClaimType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return claimType.EndsWith("/" + expectedClaimType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
