using FWO.Report.Filter;
using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    public class FilterTest
    {
        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        public void EmptySearch()
        {
            Compiler.Compile("", ReportType.Rules);
        }

        [Test]
        public void WhitespaceSearch()
        {
            Compiler.Compile("\t\n  \r  \t \n", ReportType.Rules);
        }

        [Test]
        public void TextOnlySearch()
        {
            AstNode? ast = Compiler.CompileToAst("teststring");
            DynGraphqlQuery query = Compiler.Compile("teststring", ReportType.Rules);
        }

        [Test]
        public void AndOr()
        {
            var res = Compiler.Compile("((src=hi) & (dst=test)) | (src = a)", ReportType.Rules);
        }

        [Test]
        public void TripleOr()
        {
            var res = Compiler.Compile("(src=cactus or dst=cactus or svc=smtps)", ReportType.Rules);
        }

        [Test]
        public void NotEquals()
        {
            var res = Compiler.Compile("(text!=cactus)", ReportType.Rules);
        }

        [Test]
        public void ExactEquals()
        {
            var res = Compiler.Compile("(text==cactus)", ReportType.Rules);
        }

        [Test]
        public void ExactEquals2()
        {
            var res = Compiler.Compile("(gateway = \"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst == IsoAAADray.local", ReportType.Rules);
        }

        [Test]
        public void ExactEquals3()
        {
            try
            {
                var res = Compiler.Compile("(gateway=\"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst ==", ReportType.Rules);
                Assert.Fail("Excpetion should have been thrown");
            }
            catch (SyntaxException exception)
            {
                Assert.AreEqual("No token but one was expected", exception.Message);
            }
        }

        [Test]
        public void Disabled()
        {
            var res = Compiler.Compile("disabled == true", ReportType.Rules);
        }


        [Test]
        public void Brackets()
        {
            var res = Compiler.Compile("src=a&(dst=c)", ReportType.Rules);
        }
    }
}
