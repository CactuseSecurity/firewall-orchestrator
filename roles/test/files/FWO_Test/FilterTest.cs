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
            Compiler.Compile("");
        }

        [Test]
        public void WhitespaceSearch()
        {
            Compiler.Compile("\t\n  \r  \t \n");
        }

        [Test]
        public void TextOnlySearch()
        {
            AstNode ast = Compiler.CompileToAst("teststring");
            DynGraphqlQuery query = Compiler.Compile("teststring");
        }

        [Test]
        public void AndOr()
        {
            var res = Compiler.Compile("((src=hi) & (dst=test)) | (src = a)");
        }

        [Test]
        public void TripleOr()
        {
            var res = Compiler.Compile("(src=cactus or dst=cactus or svc=smtps)");
        }

        [Test]
        public void ExactEquals()
        {
            var res = Compiler.Compile("(text==cactus)");
        }

        [Test]
        public void ExactEquals2()
        {
            var res = Compiler.Compile("(text==cactus)");
        }
    }
}
