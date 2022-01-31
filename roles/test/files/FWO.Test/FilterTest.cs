using FWO.Report.Filter;
using FWO.Report.Filter.Ast;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Test.Filter
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
        public void ExactEquals()
        {
            var res = Compiler.Compile("(text==cactus)", ReportType.Rules);
        }

        [Test]
        public void ExactEquals2()
        {
            var res = Compiler.Compile("(text==cactus)", ReportType.Rules);
        }
    }
}
