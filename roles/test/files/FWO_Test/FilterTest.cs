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
        string input = "";

        [TestInitialize]
        public void Initialize()
        {

        }

        [TestMethod]
        public void TextOnlySearch()
        {
            input = "teststring";
            Compiler.Compile(input);
        }

        [TestMethod]
        public void Easy0()
        {
            input = "((src=123) & (dst=234)) | (src = 123)";
            Compiler.Compile(input);
        }
    }
}
