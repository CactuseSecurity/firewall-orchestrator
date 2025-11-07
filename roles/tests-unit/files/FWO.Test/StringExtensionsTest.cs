using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Basics;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class StringExtensionsTest
    {
        #region StringExtensionsSanitizer
        static readonly string OkText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_";
        static readonly string TextToShorten = " A\"\\'!,; ";
        static readonly string ShortenedText = "A";
        static readonly string OkLdapName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()_";
        static readonly string LdapNameToShorten = " A+;,\"<>#= B ";
        static readonly string ShortenedLdapName = "A B";
        static readonly string OkLdapPath = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()_,=";
        static readonly string LdapPathToShorten = " A+;,\"<>#= B ";
        static readonly string ShortenedLdapPath = "A,= B";
        static readonly string OkPassw = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#";
        static readonly string PasswToShorten = " a \n\rb ";
        static readonly string ShortenedPassw = "a b";
        static readonly string OkKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_\"\\'!,;";
        static readonly string KeyToShorten = " anykey ";
        static readonly string ShortenedKey = "anykey";
        static readonly string OkComment = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz1234567890.*-:?@/()[]{}$+<>#_\\!,;";
        static readonly string CommentToShorten = "\"anytext'";
        static readonly string ShortenedComment = "anytext";
        static readonly string OkCidr = "1234567890ABCDEFabcdef:./";
        static readonly string CidrToShorten = " FGHIJKLMNOPQRSTUVWXYZ fghijklmnopqrstuvwxyz?@()[]{}$+<>#_\\!,; ";
        static readonly string ShortenedCidr = "Ff";
        static readonly string OkJson = "1234567890ABCDEFabcdef:._{a§}+*(){}[]?!#<>=,;/\\@$%^|&";
        static readonly string JsonToShorten = " {\t\n\r}";
        static readonly string ShortenedJson = "{}";
        static readonly string OkJsonField = "1234567890ABCDEFabcdef:._";
        static readonly string JsonFieldToShorten = " {a§}+*(){}[]?!#<>=,;/\\\t@$%^|&~ ";
        static readonly string ShortenedJsonField = "_a§___________________________";

        [SetUp]
        public void Initialize()
        {}

        [Test]
        public void TestSanitizer()
        {
            bool shortened = false;
            string? nullString = null;
            ClassicAssert.AreEqual(null, nullString.SanitizeOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, OkText.SanitizeOpt( ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkText, OkText.SanitizeMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedText, TextToShorten.SanitizeMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeLdapNameOpt(ref shortened));
            ClassicAssert.AreEqual(OkLdapName, OkLdapName.SanitizeLdapNameOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapName, OkLdapName.SanitizeLdapNameMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapName, LdapNameToShorten.SanitizeLdapNameMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeLdapPathOpt(ref shortened));
            ClassicAssert.AreEqual(OkLdapPath, OkLdapPath.SanitizeLdapPathOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkLdapPath, OkLdapPath.SanitizeLdapPathMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedLdapPath, LdapPathToShorten.SanitizeLdapPathMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizePasswOpt(ref shortened));
            ClassicAssert.AreEqual(OkPassw, OkPassw.SanitizePasswOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkPassw, OkPassw.SanitizePasswMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedPassw, PasswToShorten.SanitizePasswMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeKeyOpt(ref shortened));
            ClassicAssert.AreEqual(OkKey, OkKey.SanitizeKeyOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkKey, OkKey.SanitizeKeyMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedKey, KeyToShorten.SanitizeKeyMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeCommentOpt(ref shortened));
            ClassicAssert.AreEqual(OkComment, OkComment.SanitizeCommentOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkComment, OkComment.SanitizeCommentMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedComment, CommentToShorten.SanitizeCommentMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(null, nullString.SanitizeCidrOpt(ref shortened));
            ClassicAssert.AreEqual(OkCidr, OkCidr.SanitizeCidrOpt(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(OkCidr, OkCidr.SanitizeCidrMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedCidr, CidrToShorten.SanitizeCidrMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJson, OkJson.SanitizeJsonMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJson, JsonToShorten.SanitizeJsonMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);

            shortened = false;
            ClassicAssert.AreEqual(OkJsonField, OkJsonField.SanitizeJsonFieldMand(ref shortened));
            ClassicAssert.AreEqual(false, shortened);
            ClassicAssert.AreEqual(ShortenedJsonField, JsonFieldToShorten.SanitizeJsonFieldMand(ref shortened));
            ClassicAssert.AreEqual(true, shortened);
        }


        // --- SanitizeMand & SanitizeOpt ---
        [TestCase("abcDEF123._-*:?@/()[]{}$+<># ", "abcDEF123._-*:?@/()[]{}$+<>#", true)]
        [TestCase("abc!def§ghi", "abcdefghi", true)]
        public void SanitizeMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeOpt(ref shortened);
            Assert.That(result, Is.Null);
            Assert.That(shortened, Is.False);
        }

        // --- SanitizeLdapNameMand & Opt ---
        [TestCase("CN-User.Name@domain()", "CN-User.Name@domain()", false)]
        [TestCase("User!Name§", "UserName", true)]
        public void SanitizeLdapNameMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeLdapNameMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeLdapNameOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeLdapNameOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizeLdapPathMand & Opt ---
        [TestCase("CN=User,OU=Dept,DC=example,DC=com", "CN=User,OU=Dept,DC=example,DC=com", false)]
        [TestCase("CN=User§,OU=Dept!", "CN=User,OU=Dept", true)]
        public void SanitizeLdapPathMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeLdapPathMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeLdapPathOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeLdapPathOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizePasswMand & Opt ---
        [TestCase("password123", "password123", false)]
        [TestCase("pass\tword\n", "password", true)]
        public void SanitizePasswMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizePasswMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizePasswOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizePasswOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizeKeyMand & Opt ---
        [TestCase(" key ", "key", true)]
        [TestCase("clean", "clean", false)]
        public void SanitizeKeyMand_Trims(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeKeyMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeKeyOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeKeyOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizeCommentMand & Opt ---
        [TestCase("comment", "comment", false)]
        [TestCase("\"quoted\" and 'single'", "quoted and single", true)]
        public void SanitizeCommentMand_RemovesQuotes(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeCommentMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeCommentOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeCommentOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizeCidrMand & Opt ---
        [TestCase("192.168.0.1/24", "192.168.0.1/24", false)]
        [TestCase("192.168.0.1/24x", "192.168.0.1/24", true)]
        public void SanitizeCidrMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeCidrMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        [Test]
        public void SanitizeCidrOpt_Null_ReturnsNull()
        {
            string? input = null;
            bool shortened = false;
            string? result = input.SanitizeCidrOpt(ref shortened);
            Assert.That(result, Is.Null);
        }

        // --- SanitizeJsonMand ---
        [TestCase("{\"key\":\"value\"}", "{\"key\":\"value\"}", false)]
        [TestCase("{\"key\":\t\"value\"}", "{\"key\":\"value\"}", true)]
        public void SanitizeJsonMand_Works(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeJsonMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }

        // --- SanitizeJsonFieldMand ---
        [TestCase("fieldName", "fieldName", false)]
        [TestCase("field+name", "field_name", true)]
        [TestCase("test@field", "test_field", true)]
        public void SanitizeJsonFieldMand_ReplacesInvalidChars(string input, string expected, bool expectedChanged)
        {
            bool changed = false;
            string result = input.SanitizeJsonFieldMand(ref changed);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(changed, Is.EqualTo(expectedChanged));
        }

        // --- SanitizeEolMand ---
        [TestCase("line1 line2", "line1 line2", false)]
        [TestCase("line1\nline2\rline3", "line1 line2 line3", false)]
        public void SanitizeEolMand_ReplacesNewlinesWithSpaces(string input, string expected, bool expectedShortened)
        {
            bool shortened = false;
            string result = input.SanitizeEolMand(ref shortened);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(shortened, Is.EqualTo(expectedShortened));
        }
        #endregion

        #region StringExtensionsLdap
        // --- ExtractCommonNameFromDn ---
        [TestCase("CN=John Doe,OU=Users,DC=example,DC=com", "John Doe")]
        [TestCase("CN=Alice", "Alice")]
        [TestCase("OU=Users,DC=example,DC=com", "")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void ExtractCommonNameFromDn_Works(string dn, string expected)
        {
            string result = dn.ExtractCommonNameFromDn();
            Assert.That(result, Is.EqualTo(expected));
        }
        #endregion

        #region StringExtensionsIp
        // --- StripHtmlTags ---
        [TestCase("<b>bold</b>", "bold")]
        [TestCase("no tags", "no tags")]
        [TestCase("<i>italic</i><br>", "italic")]
        [TestCase("", "")]
        public void StripHtmlTags_Works(string input, string expected)
        {
            string result = input.StripHtmlTags();
            Assert.That(result, Is.EqualTo(expected));
        }

        // --- StripDangerousHtmlTags ---
        //[TestCase("<b>bold</b>", "bold")]       //Ok - no closing </    "/" or is it okay?
        [TestCase("<i>italic</i>", "<i>italic</i>")]
        [TestCase("<br>line<br>", "<br>line<br>")]
        [TestCase("<script>alert(1)</script>", "alert(1)")]
        public void StripDangerousHtmlTags_Works(string input, string expected)
        {
            string result = input.StripDangerousHtmlTags();
            Assert.That(result, Is.EqualTo(expected));
        }

        // --- IsIPv4 / IsIPv6 ---
        [TestCase("192.168.0.1", true, false)]
        [TestCase("::1", false, true)]
        [TestCase("invalid", false, false)]
        public void IsIPVersion_Works(string input, bool expectedV4, bool expectedV6)
        {
            Assert.That(input.IsIPv4(), Is.EqualTo(expectedV4));
            Assert.That(input.IsIPv6(), Is.EqualTo(expectedV6));
        }

        // --- IsV4Address / IsV6Address ---
        [TestCase("192.168.1.1", true, false)]
        [TestCase("fe80::1", false, true)]
        [TestCase("hello", false, false)]
        public void IsVxAddress_Works(string input, bool expectedV4, bool expectedV6)
        {
            Assert.That(input.IsV4Address(), Is.EqualTo(expectedV4));
            Assert.That(input.IsV6Address(), Is.EqualTo(expectedV6));
        }

        // --- StripOffNetmask / StripOffUnnecessaryNetmask / GetNetmask ---
        [TestCase("192.168.1.1/24", "192.168.1.1", "24")]
        [TestCase("192.168.1.1/32", "192.168.1.1", "32")]
        [TestCase("fe80::1/64", "fe80::1", "64")]
        [TestCase("fe80::1/128", "fe80::1", "128")]
        [TestCase("10.0.0.1", "10.0.0.1", "")]
        public void NetmaskMethods_Works(string input, string expectedStrip, string expectedNetmask)
        {
            Assert.That(input.StripOffNetmask(), Is.EqualTo(expectedStrip));
            Assert.That(input.GetNetmask(), Is.EqualTo(expectedNetmask));

            // StripOffUnnecessaryNetmask removes /32 for IPv4 and /128 for IPv6
            if (expectedNetmask == "32" || expectedNetmask == "128")
                Assert.That(input.StripOffUnnecessaryNetmask(), Is.EqualTo(expectedStrip));
            else
                Assert.That(input.StripOffUnnecessaryNetmask(), Is.EqualTo(input));
        }

        // --- TrySplit ---
        [TestCase("a,b,c", ',', 0, true, "a")]
        [TestCase("a,b,c", ',', 2, true, "c")]
        [TestCase("a,b,c", ',', 3, false, "")]
        [TestCase("single", ',', 0, false, "")]
        public void TrySplit_ByIndex_Works(string input, char sep, int index, bool expectedResult, string expectedOutput)
        {
            bool result = input.TrySplit(sep, index, out string output);
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(output, Is.EqualTo(expectedOutput));
        }

        [TestCase("a,b,c", ',', true, 3)]
        [TestCase("single", ',', false, 0)]
        public void TrySplit_ByLength_Works(string input, char sep, bool expectedResult, int expectedLength)
        {
            bool result = input.TrySplit(sep, out int length);
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(length, Is.EqualTo(expectedLength));
        }

        // --- TryGetNetmask ---
        [TestCase("192.168.1.1/24", true, "/24")]
        [TestCase("10.0.0.1", false, "")]
        public void TryGetNetmask_Works(string input, bool expectedResult, string expectedNetmask)
        {
            bool result = input.TryGetNetmask(out string netmask);
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(netmask, Is.EqualTo(expectedNetmask));
        }

        // --- Compare / IsGreater / GenerousCompare ---
        [TestCase("192.168.1.1", "192.168.1.2", -1)]
        [TestCase("192.168.1.2", "192.168.1.1", 1)]
        [TestCase("192.168.1.1", "192.168.1.1", 0)]
        public void CompareIPs_Works(string left, string right, int expected)
        {
            int result = left.Compare(right);
            Assert.That(Math.Sign(result), Is.EqualTo(Math.Sign(expected)));
        }

        [TestCase("192.168.1.2", "192.168.1.1", true)]
        [TestCase("192.168.1.1", "192.168.1.2", false)]
        public void IsGreater_Works(string left, string right, bool expected)
        {
            bool result = left.IsGreater(right);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(null, null, true)]
        [TestCase("", "", true)]
        [TestCase("abc", "abc", true)]
        [TestCase("abc", "def", false)]
        public void GenerousCompare_Works(string s1, string s2, bool expected)
        {
            bool result = s1.GenerousCompare(s2);
            Assert.That(result, Is.EqualTo(expected));
        }

        // --- CidrToRange / CidrToRangeString ---
        [TestCase("192.168.1.0/24", "192.168.1.0", "192.168.1.255")]
        [TestCase("::1/128", "::1", "::1")]
        public void CidrToRangeString_Works(string cidr, string expectedStart, string expectedEnd)
        {
            var (start, end) = cidr.CidrToRangeString();
            Assert.That(start, Is.EqualTo(expectedStart));
            Assert.That(end, Is.EqualTo(expectedEnd));

            var (ipStart, ipEnd) = cidr.CidrToRange();
            Assert.That(ipStart.ToString(), Is.EqualTo(expectedStart));
            Assert.That(ipEnd.ToString(), Is.EqualTo(expectedEnd));
        }

        // --- ReplaceAll ---
        [Test]
        public void ReplaceAll_Works()
        {
            string input = "abc def ghi";
            var values = new List<string> { "abc", "ghi" };
            string result = input.ReplaceAll(values, "X");
            Assert.That(result, Is.EqualTo("X def X"));
        }

        // --- ToIPAdressAndSubnetMask ---
        [TestCase("192.168.1.1/24", "192.168.1.1", "24")]
        [TestCase("10.0.0.1", "10.0.0.1", "")]
        public void ToIPAdressAndSubnetMask_Works(string input, string expectedIp, string expectedSubnet)
        {
            var (ip, subnet) = input.ToIPAdressAndSubnetMask();
            Assert.That(ip.ToString(), Is.EqualTo(expectedIp));
            Assert.That(subnet, Is.EqualTo(expectedSubnet));
        }
        #endregion
    }
}
