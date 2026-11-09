using System.Text.RegularExpressions;
using FWO.Basics;


namespace FWO.Api.Client.Queries
{
    public class Queries
    {
        protected static string QueryPath => Path.Combine(GetQueryBasePath(), "fwo-api-calls") + Path.DirectorySeparatorChar;

        protected static string GetQueryText(string relativeQueryFileName)
        {
            return Compact(" " + File.ReadAllText(QueryPath + relativeQueryFileName) + " ");
        }

        private static string GetQueryBasePath()
        {
            string? queryBasePath = Environment.GetEnvironmentVariable("FWO_BASE_DIR");
            return string.IsNullOrEmpty(queryBasePath) ? GlobalConst.kFwoBaseDir : queryBasePath;
        }

        public static string Compact(string raw_query)
        {
            // Split the input into lines
            var lines = raw_query.Split(new[] { '\n' }, StringSplitOptions.None);

            // Remove comments and process each line
            for (int i = 0; i < lines.Length; i++)
            {
                // Remove everything starting from '#' to the end of the line
                lines[i] = Regex.Replace(lines[i], @"#.*", "", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();
            }

            // Rejoin the lines back into a single string
            raw_query = string.Join("\n", lines);

            // Replace tabs with a single space
            raw_query = Regex.Replace(raw_query, @"\t+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();

            // Replace multiple spaces with a single space
            raw_query = Regex.Replace(raw_query, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100));

            // Remove remaining newline characters (if needed)
            raw_query = Regex.Replace(raw_query, @"[\n]", "", RegexOptions.None, TimeSpan.FromMilliseconds(100));

            return raw_query;
        }

    }
}
