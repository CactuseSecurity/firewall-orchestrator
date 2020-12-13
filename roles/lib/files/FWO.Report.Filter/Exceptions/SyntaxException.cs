using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter.Exceptions
{
    public class SyntaxException : FilterException
    {
        public SyntaxException(string message, Range errorPosition) : base(message, errorPosition)
        {

        }
    }
}
