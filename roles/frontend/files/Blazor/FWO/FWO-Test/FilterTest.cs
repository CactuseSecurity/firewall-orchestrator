using FWO_Filter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace FWO_Test
{
    [TestClass]
    public class FilterTest
    {
        [ExpectedException(typeof(SyntaxErrorException), "No token but one was expected")]
        [TestMethod]
        public void FilterSrcEqualsEmpty()
        {
            Compiler.Compile("src = ");
        }

        [TestMethod]
        [ExpectedException(typeof(SyntaxErrorException), "No token but one was expected")]
        public void FilterSrcEqualsNoParameter()
        {
            Compiler.Compile("src =");
        }

        [TestMethod]
        [ExpectedException(typeof(SyntaxErrorException), "Unexpected token Text: \"=\" Kind: \"EQ\"")]
        public void FilterSrcEqualsEquals()
        {
            Compiler.Compile("src = =");
        }

        [TestMethod]
        [ExpectedException(typeof(SyntaxErrorException), "No token but one was expected")]
        public void FilterEmpty()
        {
            Compiler.Compile("");
        }
    }
}
