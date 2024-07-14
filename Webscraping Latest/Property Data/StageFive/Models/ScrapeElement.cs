using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StageFive.Models
{
    internal class ScrapeElement
    {
        public IWebElement AddressElement { get; internal set; }
        public IWebElement? FloorAreaElement { get; internal set; }
        public IWebElement? BedElement { get; internal set; }
        public IWebElement? ChairElement { get; internal set; }
        public ReadOnlyCollection<IWebElement> Elements { get; internal set; }
        //public List<IWebElement> HighPriceList { get; internal set; }
        //public List<IWebElement> LowPriceList { get; internal set; }

        public IWebElement HighPriceItem { get; internal set; }
        public IWebElement LowPriceItem { get; internal set; }

        //public IWebElement SoldDateElement { get; internal set; }

        //public IWebElement SoldPriceElement { get; internal set; }

        public IWebElement? BathElement { get; internal set; }

        public IWebElement SqMetres { get; internal set; }


        public List<IWebElement>? SoldElements { get; internal set; }  



    }
}
