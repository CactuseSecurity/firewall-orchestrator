using FWO.Report.Filter;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data.Report;
namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class FilterTest
    {
        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        [Parallelizable]
        public void EmptySearch()
        {
            ReportTemplate t = new()
            {
                Filter = ""
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);
            
            ClassicAssert.AreEqual(0, query.QueryVariables.Count);
        }

        [Test]
        [Parallelizable]
        public void WhitespaceSearch()
        {
            ReportTemplate t = new()
            {
                Filter = "\t\n  \r  \t \n"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(0, query.QueryVariables.Count);
        }

        [Test]
        [Parallelizable]
        public void TextOnlySearch()
        {
            ReportTemplate t = new()
            {
                Filter = "teststring"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            Compiler.CompileToAst("teststring");
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%teststring%", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void AndOr()
        {
            ReportTemplate t = new()
            {
                Filter = "((src=hi) & (dst=test)) | (src = a)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%hi%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%test%", query.QueryVariables["dst1"]);
            ClassicAssert.AreEqual("%a%", query.QueryVariables["src2"]);
        }

        [Test]
        [Parallelizable]
        public void TripleOr()
        {
            ReportTemplate t = new()
            {
                Filter = "(src=cactus or dst=cactus or svc=smtps)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);
            
            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%cactus%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%cactus%", query.QueryVariables["dst1"]);
            ClassicAssert.AreEqual("%smtps%", query.QueryVariables["svc2"]);
        }

        [Test]
        [Parallelizable]
        public void NotEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "(text!=cactus)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);
            
            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("cactus", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "(text==cactus)"
            };
            t.ReportParams.ReportType = (int) ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("cactus", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals2()
        {
            ReportTemplate t = new()
            {
                Filter = "(gateway = \"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst == IsoAAADray.local"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);
            
            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%checkpoint_demo%", query.QueryVariables["gwName0"]);
            ClassicAssert.AreEqual("%fortigate_demo%", query.QueryVariables["gwName1"]);
            ClassicAssert.AreEqual("IsoAAADray.local", query.QueryVariables["dst2"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals3()
        {
            try
            {
                ReportTemplate t = new()
                {
                    Filter = "(gateway=\"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst =="
                };
                t.ReportParams.ReportType = (int) ReportType.Rules;
                Compiler.Compile(t);
                Assert.Fail("Exception should have been thrown");
            }
            catch (SyntaxException exception)
            {
                ClassicAssert.AreEqual("No token but one was expected", exception.Message);
            }
        }

        [Test]
        [Parallelizable]
        public void Disabled()
        {
            ReportTemplate t = new()
            {
                Filter = "disabled == true"
            };
            t.ReportParams.ReportType = (int) ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("true", query.QueryVariables["disabled0"]);
       }

        [Test]
        [Parallelizable]
        public void Brackets()
        {
            ReportTemplate t = new()
            {
                Filter = "src=a&(dst=c)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(2, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%a%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%c%", query.QueryVariables["dst1"]);
        }
    }
}
