using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter.Exceptions
{
    public class FilterException : Exception
    {
        public readonly Range ErrorPosition;

        public FilterException(string message, Range errorPosition) : base(message)
        {
            ErrorPosition = errorPosition;
        }
    }
}
