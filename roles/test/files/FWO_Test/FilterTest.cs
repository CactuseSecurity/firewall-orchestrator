using FWO.Ui.Filter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Test.Filter
{
    [TestClass]
    public class FilterTest
    {
        [TestInitialize]
        public void Initialize()
        {

        }

        [TestMethod]
        public void EmptySearch()
        {
            Compiler.Compile("");
        }

        [TestMethod]
        public void WhitespaceSearch()
        {
            Compiler.Compile("\t\n  \r  \t \n");
        }

        [TestMethod]
        public void TextOnlySearch()
        {
            Compiler.Compile("teststring");
        }

        [TestMethod]
        public void AndOr()
        {
            Compiler.Compile("((src=123) & (dst=234)) | (src = 123)");
        }

        public void TripleOr()
        {
            Compiler.Compile("(src=cactus or dst=cactus or svc=smtps)");
        }
        
    }
}
