using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Numerics;
using NUnit.Framework;
using NetTools;
using FWO.Basics.Comparer;

namespace FWO.Test
{
    [TestFixture]
    public class IPAddressRangeComparerTests
    {
        [Test]
        public void GetIPRangeSizeTest()
        {
            // ARRANGE

            IPAddressRangeComparer comparer = new ();

            Stopwatch stopwatch;

            int iterations = 1;

            var testCases = new[]
            {
                // IPv4 little range
                // expected result: new BigInteger(255)
                new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255")),

                // IPv4 big range
                // expected result: new BigInteger(16_777_215)
                new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.255.255.255")),

                // IPv6 little range
                // expected result: new BigInteger(1)
                new IPAddressRange(IPAddress.Parse("::1"), IPAddress.Parse("::2")),                  

                // IPv6 medium range
                // expected result: new BigInteger(65_535)
                new IPAddressRange(IPAddress.Parse("2001:db8::"), IPAddress.Parse("2001:db8::ffff")), 

                // IPv6 big range
                // expected result: BigInteger.Parse("340282366920938463463374607431768211455") 
                new IPAddressRange(IPAddress.Parse("::"), IPAddress.Parse("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")) 
            };

            // key: iteration, testcase
            // value: result, performance
            Dictionary<(int ,IPAddressRange), (BigInteger, TimeSpan)> testResults = new ();

            // ACT

            for (int i = 1; i <= iterations; i++)
            {
                foreach (var range in testCases)
                {
                    // execute method
                    stopwatch = Stopwatch.StartNew();
                    BigInteger result = comparer.GetIPRangeSize(range);
                    stopwatch.Stop();

                    // save test results
                    testResults[(i, range)] = (result, stopwatch.Elapsed);
                }            
            }

            // ASSERT

            var testCase1Results = testResults.Where(kvp => kvp.Key.Item2 == testCases[0]);
            Assert.That(testCase1Results.Select(kvp => kvp.Value.Item1),
                        Is.All.EqualTo(new BigInteger(255)));

            var testCase2Results = testResults.Where(kvp => kvp.Key.Item2 == testCases[1]);
            Assert.That(testCase2Results.Select(kvp => kvp.Value.Item1),
                        Is.All.EqualTo(new BigInteger(16_777_215)));

            var testCase3Results = testResults.Where(kvp => kvp.Key.Item2 == testCases[2]);
            Assert.That(testCase3Results.Select(kvp => kvp.Value.Item1),
                        Is.All.EqualTo(new BigInteger(1)));

            var testCase4Results = testResults.Where(kvp => kvp.Key.Item2 == testCases[3]);
            Assert.That(testCase4Results.Select(kvp => kvp.Value.Item1),
                        Is.All.EqualTo(new BigInteger(65_535)));    

            var testCase5Results = testResults.Where(kvp => kvp.Key.Item2 == testCases[4]);
            Assert.That(testCase5Results.Select(kvp => kvp.Value.Item1),
                        Is.All.EqualTo(BigInteger.Parse("340282366920938463463374607431768211455")));    
        }
    }
}
