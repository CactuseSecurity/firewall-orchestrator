using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    public struct NavItem
    {
        /// <summary>
        /// Displayed Nav Item Name
        /// </summary>
        public readonly Func<string> Name;

        /// <summary>
        /// Link to navigate when clicked.
        /// </summary>
        public readonly string Link;

        /// <summary>
        /// Displayed Nav Symbol
        /// </summary>
        public readonly string Symbol;
        
        public NavItem(Func<string> Name, string Link, string Symbol)
        {
            this.Name = Name;
            this.Link = Link;
            this.Symbol = Symbol;
        }
    }
}
