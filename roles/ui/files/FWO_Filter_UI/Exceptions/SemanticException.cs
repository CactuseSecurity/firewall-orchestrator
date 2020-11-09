using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Exceptions
{
    public class SemanticException : FilterException
    {
        public SemanticException(string message, Range errorPosition) : base(message, errorPosition)
        {

        }
    }
}
