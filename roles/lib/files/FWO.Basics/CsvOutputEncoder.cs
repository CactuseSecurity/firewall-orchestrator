using System.Globalization;

namespace FWO.Basics
{
    /// <summary>
    /// Encodes values for the cells of CSV exports. Cell values can come from imported firewall data, LDAP or user
    /// input, and a spreadsheet opening the export would otherwise run a value that starts like a formula.
    /// </summary>
    public static class CsvOutputEncoder
    {
        private const char kFormulaGuard = '\'';
        private const string kQuote = "\"";
        private const string kEscapedQuote = "\"\"";

        /// <summary>Characters that make a spreadsheet read a cell as a formula (OWASP CSV injection).</summary>
        private static readonly List<char> kFormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

        /// <summary>
        /// Encodes a value as one quoted CSV cell: quotes inside are doubled, so that the value cannot end its cell,
        /// and a leading formula character is neutralised.
        /// </summary>
        /// <param name="value">The value to write, null is treated as empty.</param>
        /// <returns>The quoted cell without separator.</returns>
        public static string EncodeCell(string? value)
        {
            string cell = NeutralizeFormula(value ?? "");
            return kQuote + cell.Replace(kQuote, kEscapedQuote) + kQuote;
        }

        /// <summary>
        /// Prefixes a value that a spreadsheet would read as a formula with a single quote, so that it is shown as
        /// text. Plain numbers such as "-5" and single characters cannot do harm and stay unchanged.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>The value, prefixed if it could start a formula.</returns>
        public static string NeutralizeFormula(string value)
        {
            if (value.Length <= 1 || !kFormulaPrefixes.Contains(value[0]) || IsPlainNumber(value))
            {
                return value;
            }

            return kFormulaGuard + value;
        }

        private static bool IsPlainNumber(string value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
        }
    }
}
