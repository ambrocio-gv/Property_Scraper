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
        public List<Models.Proxy> GetProxies(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var nodes = htmlDoc.DocumentNode.Descendants("table").FirstOrDefault()?.ChildNodes
                .Where(x => x.Name == "tbody").FirstOrDefault()?.ChildNodes.ToList();

        

            var proxies = new List<Models.Proxy>();


            if (nodes is not null)
            {
                foreach (var node in nodes)
                {
                    var ipAddress = node.ChildNodes[0].InnerText.Trim();
                    var port = node.ChildNodes[1].InnerText.Trim();
                    var country = node.ChildNodes[3].InnerText.Trim();

                    //var isGoogle = node.ChildNodes[5].InnerText.Trim().ToLower();
                    //if (isGoogle == "no") continue;
                    proxies.Add(new Models.Proxy { IpAddress = ipAddress, Port = port, Country = country });


                }
            }
            return proxies;
        }


    }
}