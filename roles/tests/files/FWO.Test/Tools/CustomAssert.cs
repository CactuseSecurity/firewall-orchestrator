using System;
using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test.Tools.CustomAssert
{
    public static class AssertWithDump
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            IncludeFields = true,
            MaxDepth = int.MaxValue
        };

        /// <summary>
        /// Asserts that two strings are equal. If they are not, it attempts to parse them as JSON and pretty-print them for easier comparison.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <param name="message"></param>
        public static void AreEqual(string expected, string actual, string? message = null)
        {
            try
            {
                Assert.That(actual, Is.EqualTo(expected), message);
            }
            catch (AssertionException ex)
            {
                TestContext.WriteLine("=== ASSERTION FAILED ===\n");
                if (!string.IsNullOrWhiteSpace(message))
                    TestContext.WriteLine(message);
                TestContext.WriteLine(ex.Message);

                try
                {
                    var expectedParsed = JsonSerializer.Deserialize<JsonElement>(expected);
                    var actualParsed = JsonSerializer.Deserialize<JsonElement>(actual);

                    TestContext.WriteLine("\n=== EXPECTED ===");
                    TestContext.WriteLine(JsonSerializer.Serialize(expectedParsed, JsonOptions));

                    TestContext.WriteLine("\n=== ACTUAL ===");
                    TestContext.WriteLine(JsonSerializer.Serialize(actualParsed, JsonOptions));
                }
                catch (JsonException)
                {
                    // If no valid JSON just print raw strings

                    TestContext.WriteLine("\n=== EXPECTED (raw) ===");
                    TestContext.WriteLine(expected);

                    TestContext.WriteLine("\n=== ACTUAL (raw) ===");
                    TestContext.WriteLine(actual);
                }

                throw;
            }
        }
    }
}
