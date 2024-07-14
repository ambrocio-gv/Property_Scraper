using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using StageOne.Models;
using System.Reflection;

namespace StageOne
{
    internal class ProxyRepository
    {
        public List<Models.Proxy> GetProxies(string proxysite, ChromeDriver driver, string country)
        {
            //HtmlDocument htmlDoc = new HtmlDocument();
            //htmlDoc.LoadHtml(html);

            //var nodes = htmlDoc.DocumentNode.Descendants("table").FirstOrDefault()?.ChildNodes
            //    .Where(x => x.Name == "tbody").FirstOrDefault()?.ChildNodes.ToList();




            //if (nodes is not null)
            //{
            //    foreach (var node in nodes)
            //    {
            //        var ipAddress = node.ChildNodes[0].InnerText.Trim();
            //        var port = node.ChildNodes[1].InnerText.Trim();
            //        var country = node.ChildNodes[3].InnerText.Trim();

            //        //var isGoogle = node.ChildNodes[5].InnerText.Trim().ToLower();
            //        //if (isGoogle == "no") continue;
            //        proxies.Add(new Models.Proxy { IpAddress = ipAddress, Port = port, Country = country });


            //    }
            //}



            driver.Navigate().GoToUrl(proxysite);
           
            driver.Manage().Window.Maximize();
            var proxies = new List<Models.Proxy>();










            //var nextspan = driver.FindElements(By.XPath("//span[text()='Next »']"));
            var nextspan = driver.FindElements(By.XPath("//span[contains(text(),'Next »')]"));

            while (nextspan.Count() == 0)
            {


                //Thread.Sleep(5000);


                //var Ad = driver.FindElements(By.Id("ad_position_box"));

                //var dismissAd = driver.FindElements(By.Id("dismiss-button"));
                //if(dismissAd.Count() > 0)
                //{
                //    dismissAd[0].Click();
                //}

                //var dismissClose = driver.FindElements(By.XPath("//span[text()='Close']"));
                //if(dismissClose.Count() > 0)
                //{
                //    dismissClose[0].Click();
                //}


                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("const elements = document.getElementsByClassName('adsbygoogle adsbygoogle-noablate'); while (elements.length > 0) elements[0].remove()");




                var export = driver.FindElement(By.XPath("//*[@id='clickexport']"));

                export.Click();

                var ipString = driver.FindElement(By.Id("zkzk")).Text;

                var ipList = ipString.Split("\r\n");

                foreach (var ip in ipList)
                {
                    var ipSplit = ip.Split(":");
                    string ipAddress = ipSplit[0];
                    string ipPort = ipSplit[1];

                    proxies.Add(new Models.Proxy { IpAddress = ipAddress, Port = ipPort, Country = country });

                }

                Thread.Sleep(2000);
                export.Click();


                var nextbuttons = driver.FindElements(By.XPath("//a[text()='Next »']"));
                if(nextbuttons.Count() != 0)
                {
                    nextbuttons[0].Click();
                }


                nextspan = driver.FindElements(By.XPath("//span[contains(text(),'Next »')]"));


            }


            driver.Close();




            return proxies;
        }


    }
}