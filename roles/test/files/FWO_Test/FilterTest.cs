﻿using FWO.Report.Filter;
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
            Compiler.Compile("((src=123) & (dst=234)) | (src = 123)");
        }

        [Test]
        public void TripleOr()
        {
            Compiler.Compile("(src=cactus or dst=cactus or svc=smtps)");
        }
    }
}
