using FWO.Basics;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class CsvOutputEncoderTest
    {
        [TestCase("=HYPERLINK(\"http://x\",\"y\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\"")]
        [TestCase("+cmd|' /C calc'!A0", "\"'+cmd|' /C calc'!A0\"")]
        [TestCase("-2+3", "\"'-2+3\"")]
        [TestCase("@SUM(A1)", "\"'@SUM(A1)\"")]
        [TestCase("\t=1", "\"'\t=1\"")]
        [TestCase("\r=1", "\"'\r=1\"")]
        public void EncodeCell_FormulaPrefix_IsNeutralised(string value, string expected)
        {
            Assert.That(CsvOutputEncoder.EncodeCell(value), Is.EqualTo(expected));
        }

        [TestCase("-5", "\"-5\"")]
        [TestCase("+1.5", "\"+1.5\"")]
        [TestCase("-", "\"-\"")]
        [TestCase("10.0.0.1/32", "\"10.0.0.1/32\"")]
        [TestCase("a\"b", "\"a\"\"b\"")]
        [TestCase(null, "\"\"")]
        public void EncodeCell_HarmlessValue_IsOnlyQuoted(string? value, string expected)
        {
            Assert.That(CsvOutputEncoder.EncodeCell(value), Is.EqualTo(expected));
        }

        [Test]
        public void EncodeCell_QuoteBreakout_StaysInOneCell()
        {
            string cell = CsvOutputEncoder.EncodeCell("x\",\"=1+1");

            Assert.That(cell, Is.EqualTo("\"x\"\",\"\"=1+1\""));
        }

        [Test]
        public void RuleDisplayCsv_OutputCsv_UsesTheEncoder()
        {
            RuleDisplayCsv ruleDisplayCsv = new(new SimulatedUserConfig());

            string cell = ruleDisplayCsv.OutputCsv("=1+1\",\"x");

            Assert.That(cell, Is.EqualTo("\"'=1+1\"\",\"\"x\","));
        }
    }
}
