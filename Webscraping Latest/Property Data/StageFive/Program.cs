using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace StageFive
{
    public class Program
    {
        private static bool runHeadless = true;
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S5-" + Environment.MachineName;
        static By SelectorByAttributeValue(string p_strAttributeName, string p_strAttributeValue)
        {
            return (By.XPath(String.Format("/[@{0} = '{1}']",
                                           p_strAttributeName,
                                           p_strAttributeValue)));
        }
        static IWebElement? GetElementFromIcon(List<IWebElement> properiesElements)
        {
            if (properiesElements != null)
            {
                var pTags = properiesElements.FirstOrDefault()?.FindElements(By.XPath("following-sibling::*")).ToList();

                if (pTags != null)
                {
                    return pTags.FirstOrDefault();
                }
            }
            return null;
        }


        static void Main(string[] args)
        {


            StoreLog(dateTimeToday, "Start Program");
            StoreLog(dateTimeToday, "Getting Proxies and UPRNs");
            //eto
            var uprns = GetUprns();

            if (uprns is null) return;
            try
            {
                var chromeDriverService = ChromeDriverService.CreateDefaultService();

                foreach (var uprn in uprns)
                {
                    var proxies = GetProxies();

                    int totalProxies = proxies.Count();

                    if (totalProxies == 0) break;

                    // Check if the user wants to disable headless mode
                    if (args.Length > 0 && args[0].Equals("--showBrowser", StringComparison.OrdinalIgnoreCase))
                    {
                        runHeadless = false;
                    }

                    var chromeOption = new ChromeOptions();


                    int i = 0;
                    foreach (var proxy in proxies)
                    {
                        i++;
                        if ((i % 3) == 0)
                        {
                            chromeOption = GetLocalChromeOptions();
                            proxy.ProxyIP = "Local IP";
                        }

                        else
                        {
                            chromeOption = GetChromeOptions(proxy.ProxyIP, proxy.Port);
                        }

                        chromeOption = GetLocalChromeOptions();


                        object path;
                        path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

                        if (path != null)
                            chromeOption.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;

                        if (runHeadless)
                        {
                            //chromeOption.AddArgument("--headless=new");
                        }

                        StoreLog(dateTimeToday, $"Checking UPRN:{uprn}, Proxy IP:{proxy.ProxyIP}");
                        Console.WriteLine($"Checking UPRN:{uprn}, Proxy IP:{proxy.ProxyIP} @{DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt")}");

                        var driver = GetChromeDriver(chromeDriverService, chromeOption);

                        var url = $"https://www.zoopla.co.uk/property/uprn/{uprn}/";
                        //var url = $"https://www.zoopla.co.uk/property/uprn/10009749560/";



                        try
                        {
                            var proxyResult = GetProxyResult(driver, url);


                            if (proxyResult == "NoRespond" || proxyResult == "Blocked" || proxyResult == "Cloudflare")
                            {

                                driver.Quit();
                                UpdateProxyIP(proxy.Id, proxyResult);

                            }
                            else
                            {
                                //UpdateProxyIP(proxy.Id, "Success");

                                try
                                {

                                    var scrapeElement = GetScrapedData(driver);
                                    if (scrapeElement.AddressElement is null)
                                    {
                                        driver.Quit();
                                        break;
                                    }

                                    //IWebElement? lowPrice = GetHighLowPriceElement(scrapeElement.LowPriceList);
                                    //IWebElement? highPrice = GetHighLowPriceElement(scrapeElement.HighPriceList);

                                    IWebElement? lowPrice = scrapeElement.LowPriceItem;
                                    IWebElement? highPrice = scrapeElement.HighPriceItem;
                                    List<IWebElement>? soldElements = new List<IWebElement>();

                                    if (scrapeElement.SoldElements is not null)
                                    {
                                        soldElements = scrapeElement.SoldElements;
                                    }




                                    AddProperty(scrapeElement.AddressElement,
                                        scrapeElement.BedElement,
                                        scrapeElement.BathElement,
                                        scrapeElement.ChairElement,
                                        scrapeElement.FloorAreaElement,
                                        scrapeElement.Elements.FirstOrDefault(),
                                        lowPrice,
                                        highPrice,
                                        scrapeElement.SqMetres,
                                        uprn,
                                        url,

                                        soldElements
                                        );



                                    driver.Quit();

                                }
                                catch (Exception ex)
                                {
                                    driver.Quit();
                                }

                                break;
                            }

                        }
                        catch (Exception ex)
                        {
                            StoreLog(dateTimeToday, ex.Message + "\n\n" + ex.StackTrace);
                            driver.Quit();

                            var processes = Process.GetProcessesByName("chromedriver.exe");
                            foreach (Process process in processes)
                            {
                                process.Kill();
                            }
                        }

                        //Mix in Office IP every after 2 proxy IPs      
                        driver.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                StoreLog(dateTimeToday, ex.Message + "\n\n" + ex.StackTrace);
            }

            StoreLog(dateTimeToday, "End Session");
            Environment.Exit(0);

        }

        #region functions

        private static IWebElement? GetHighLowPriceElement(List<IWebElement> webElements)
        {
            IWebElement? webElement = null;
            if (webElements != null)
            {
                var pTags = webElements.FirstOrDefault()?.FindElements(By.TagName("p"));
                if (pTags != null)
                {
                    webElement = pTags.LastOrDefault();
                }
            }
            return webElement;
        }



        static void Print(object o)
        {
            var propinfo = o.GetType().GetProperties();

            Console.WriteLine($"-----{o.GetType().Name}-----");

            foreach (var prop in propinfo)
                Console.WriteLine($"{prop.Name}: {prop.GetValue(o)}");

            Console.WriteLine("----------");
        }


        private static Models.ScrapeElement GetScrapedData(ChromeDriver driver)
        {
            var scrapeElement = new Models.ScrapeElement();
            try
            {
                //var timeline = driver.FindElements(By.XPath("//section[contains(@id, 'timeline')]/ul/li")).Count();

                //Console.WriteLine(timeline);

                //var timeline = driver.FindElement(SelectorByAttributeValue("data-testid", "timeline"));




                //foreach (var x in timelineElements)
                //{
                //    Print(x);
                //}


                var addressFilter = "//html/body/div[3]/div/div[2]/main/div[2]/div/div/div[1]/div/section[1]/div[1]/h1";
                var addressElement = driver.FindElement(By.XPath(addressFilter));


                var floorAreaElement = GetElementFromIcon(driver.FindElements(SelectorByAttributeValue("data-testid", "floor-area-icon")).ToList());
                var bedElement = GetElementFromIcon(driver.FindElements(SelectorByAttributeValue("data-testid", "bed")).ToList());
                var bathElement = GetElementFromIcon(driver.FindElements(SelectorByAttributeValue("data-testid", "bath")).ToList());
                var chairElement = GetElementFromIcon(driver.FindElements(SelectorByAttributeValue("data-testid", "chair")).ToList());
                var elements = driver.FindElements(SelectorByAttributeValue("data-testid", "property-tags"));
                //var highPriceList = driver.FindElements(SelectorByAttributeValue("aria-label", "High estimate")).ToList();
                //var lowPriceList = driver.FindElements(SelectorByAttributeValue("aria-label", "Low estimate")).ToList();

                var lowPrice = driver.FindElement(By.XPath("//*[@id='property-estimate']/div[2]/p[1]/span[2]"));
                var highPrice = driver.FindElement(By.XPath("//*[@id='property-estimate']/div[2]/p[3]/span[2]"));

                //var sqMetres = driver.FindElement(By.XPath("//*[@id='main-content']/div[2]/div/div/div[1]/div/section[1]/div[2]/div/div/p[2]"));

                var sqMetres = GetElementFromIcon(driver.FindElements(SelectorByAttributeValue("data-testid", "floor-area-icon")).ToList());



                var timelineElements = driver.FindElements(By.XPath("//section[contains(@id, 'timeline')]/ul/li")).ToList();


                scrapeElement.AddressElement = addressElement;
                scrapeElement.FloorAreaElement = floorAreaElement;
                scrapeElement.BedElement = bedElement;
                scrapeElement.BathElement = bathElement;
                scrapeElement.ChairElement = chairElement;
                scrapeElement.Elements = elements;
                scrapeElement.HighPriceItem = highPrice;
                scrapeElement.LowPriceItem = lowPrice;
                scrapeElement.SqMetres = sqMetres;
                scrapeElement.SoldElements = timelineElements;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return scrapeElement;
        }






        private static void AddProperty(
            IWebElement? addressElement,
            IWebElement? numberOfBedrooms,
            IWebElement? numberOfBathrooms,
            IWebElement? numberOfReceptions,
            IWebElement? numberOfArea,
            IWebElement? elements,
            IWebElement? lowPrice,
            IWebElement? highPrice,
            IWebElement? sqMetres,
            string uprn,
            string webUrl,
            List<IWebElement> soldElements)
        {
            try
            {
                var propertyDetail = new Models.PropertyDetail();

                propertyDetail.UPRN = uprn;
                propertyDetail.Address = addressElement?.Text.Trim();
                propertyDetail.WebUrl = webUrl;

                propertyDetail.NumberOfBedroom = "";
                propertyDetail.NumberOfBathroom = "";
                propertyDetail.NumberOfReceptionArea = "";

                if (numberOfBedrooms is not null)
                {
                    propertyDetail.NumberOfBedroom = numberOfBedrooms.Text.ToString();
                }
                if (numberOfBathrooms is not null)
                {
                    propertyDetail.NumberOfBathroom = numberOfBathrooms.Text.ToString();
                }
                if (numberOfReceptions is not null)
                {
                    propertyDetail.NumberOfReceptionArea = numberOfReceptions.Text.ToString();
                }

                //if (soldDate is not null)
                //{ 
                //    propertyDetail.SoldDate = soldDate.Text.ToString();
                //}

                //if (soldPrice is not null)
                //{
                //    propertyDetail.SoldPrice = soldPrice.Text.ToString();
                //}


                var lowPriceText = lowPrice?.Text.Trim();
                var highPriceText = highPrice?.Text.Trim();

                propertyDetail.EstPriceRangeLow = lowPriceText;
                propertyDetail.EstPriceRangeHigh = highPriceText;

                if (elements is not null)
                {
                    var houseTypeFilter = "//*[@id='main-content']/div[2]/div/div/div[1]/div/section[1]/div[3]/div[1]/div";
                    var tenureFilter = "//*[@id='main-content']/div[2]/div/div/div[1]/div/section[1]/div[3]/div[2]/div";

                    var houseTypeElement = elements.FindElement(By.XPath(houseTypeFilter));
                    var tenureElement = elements.FindElement(By.XPath(tenureFilter));
                    var houseType = "";
                    var tenure = "";
                    if (tenureElement != null)
                    {
                        houseType = tenureElement.Text.Trim();
                    }

                    if (houseTypeElement != null)
                    {
                        tenure = houseTypeElement.Text.Trim();
                    }

                    propertyDetail.HouseType = houseType;
                    propertyDetail.Tenure = tenure;

                    if (sqMetres != null)
                    {

                        propertyDetail.SqMetres = (sqMetres.Text).Replace("sq. metres", "").Trim();
                    }
                }







                propertyDetail.SoldData = AddSoldProperty(soldElements, propertyDetail.UPRN);








                AddPropertyDetails(propertyDetail);
                AddSoldPropertyDetails(propertyDetail);



            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        static List<(string, string)>? AddSoldProperty(List<IWebElement> timelineElements, string uprn)
        {



            if (timelineElements.Count() > 0)
            {

                //li[n]/div/div[1]/div/div - sold
                //li[n]/div/p - date
                //li[n]/div/div[2]/p[1] - price


                List<(string, string)> timelineList = new List<(string, string)>();


                timelineList = timelineElements.Select(
                     element =>
                     {
                         var ifSoldElement = element.FindElement(By.XPath(".//div/div[1]/div/div"));

                         if (ifSoldElement.Text == "SOLD")
                         {
                             var soldDate = element.FindElement(By.XPath(".//div/p")).Text;
                             var soldPrice = element.FindElement(By.XPath(".//div/div[2]/p[1]")).Text;

                             return (soldDate, soldPrice);
                         }
                         else
                         {
                             return ("", "");
                         }


                     }).Where(tuple => tuple.Item1.Length > 0 && tuple.Item2.Length > 0).ToList();


                return timelineList;





                //List<string> timelineList = new List<string>();


                //foreach (var element in timelineElements)
                //{
                //    var ifSoldElement = element.FindElement(By.XPath(".//div/div[1]/div/div"));
                //    var isSold = ifSoldElement.Text;

                //    if (ifSoldElement.Text == "SOLD")
                //    {
                //        Print(ifSoldElement);


                //        string soldDate = element.FindElement(By.XPath(".//div/p")).Text;
                //        string soldPrice = element.FindElement(By.XPath(".//div/div[2]/p[1]")).Text;

                //        string soldItem = soldDate + "," + soldPrice;

                //        timelineList.Add(soldItem);
                //    }
                //}


                //}





            }
            else
            {
                return null;
            }
        }




        static List<string> GetUprns()
        {
            var list = new List<string>();
            using var context = new StageFiveContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                var spName = AppSettingsJsonParser.GetStoredProcedureName();
                cmd.CommandText = $"EXEC {spName}";
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(reader.GetString(0));
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                connection.Close();

            }
            return list;
        }
        static List<Models.Proxy> GetProxies()
        {
            var list = new List<Models.Proxy>();
            using var context = new StageFiveContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_GetProxyIP";
                var reader = cmd.ExecuteReader();

                int i = 1;
                while (reader.Read())
                {
                    list.Add(new Models.Proxy
                    {
                        Id = reader.GetInt32(0),
                        ProxyIP = reader.GetString(1),
                        Port = reader.GetString(2)
                    });

                    //added to increase proxy count if adding local IP every 2 counts
                    if ((i % 3) == 0)
                    {
                        list.Add(new Models.Proxy { Id = 0, ProxyIP = "Local IP", Port = "" });
                    }
                    i++;
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                connection.Close();
            }

            return list;
        }
        static ChromeDriver GetChromeDriver(ChromeDriverService chromeDriverService, ChromeOptions chromeOptions)
        {
            new DriverManager().SetUpDriver(new ChromeConfig());
            var driver = new ChromeDriver(chromeDriverService, chromeOptions);
            driver.Manage().Window.Maximize();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(15);

            return driver;
        }
        static ChromeOptions GetChromeOptions(string? ipAddress, string? port)
        {

            var chromeOptions = new ChromeOptions();
            //chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;


            chromeOptions.AddArguments(
                "--proxy-server=" + ipAddress + ":" + port,
                "no-sandbox");

            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
            chromeOptions.AddArgument("ignore-ssl-errors=yes");
            chromeOptions.AddArgument("ignore-certificate-errors");
            chromeOptions.AddArgument("allow-running-insecure-content");
            chromeOptions.AddArgument("--auto-open-devtools-for-tabs");
            chromeOptions.AddArgument("--user-agent=\"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\"");

            return chromeOptions;

        }
        static ChromeOptions GetLocalChromeOptions()
        {

            var chromeOptions = new ChromeOptions();
            //chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;

            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
            chromeOptions.AddArgument("ignore-ssl-errors=yes");
            chromeOptions.AddArgument("ignore-certificate-errors");
            chromeOptions.AddArgument("allow-running-insecure-content");
            chromeOptions.AddArgument("--auto-open-devtools-for-tabs");
            chromeOptions.AddArgument("--user-agent=\"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\"");

            return chromeOptions;

        }

        static void UpdateProxyIP(int? id, string? status)
        {
            var idParam = new SqlParameter("@ID", id);
            var statusParam = new SqlParameter("@Status", status);

            using var context = new StageFiveContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdateProxyIP @ID, @Status";

                cmd.Parameters.Add(idParam);
                cmd.Parameters.Add(statusParam);

                var reader = cmd.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                connection.Close();
            }
        }
        static string GetProxyResult(ChromeDriver driver, string url)
        {
            try
            {
                driver.Navigate().GoToUrl(url);

                //get response headers





                if (driver.FindElements(By.XPath("/span[text()='Accept all cookies']")).Count() != 0)
                {
                    driver.FindElement(By.Id("Save")).Click();
                }


                var filter = "//html/body/div[3]/div/div[2]/main/div[2]/div/div/div[1]/div/section[1]/div[1]/h1";


                var errorCode = driver.FindElements(By.ClassName("error-code")).Count();
                if (errorCode != 0) return "Error Code";


                var headerAddress = driver.FindElements(By.XPath(filter)).Count();
                if (headerAddress != 0) return "Success";


                var cloudflare = driver.FindElements(By.XPath("//a[text()='Cloudflare']")).Count();


                if (cloudflare != 0) return "Cloudflare";





                var forbiddenAddress = driver.FindElements(By.XPath("//h1[text()='403 Forbidden']")).Count();
                var accessDenied = driver.FindElements(By.XPath("//h1[text()='Access Denied']")).Count();

                if (forbiddenAddress != 0 || accessDenied != 0) return "Blocked";
            }
            catch
            {
                return "NoRespond";
            }
            return "NoRespond";
        }
        static bool AddPropertyDetails(Models.PropertyDetail propertyDetail)
        {
            var uprn = new SqlParameter("@UPRN", propertyDetail.UPRN);
            var address = new SqlParameter("@Address", propertyDetail.Address);
            var houseType = new SqlParameter("@HouseType", propertyDetail.HouseType);
            var tenure = new SqlParameter("@Tenure", propertyDetail.Tenure);
            var numberOfBedroom = new SqlParameter("@NumberOfBedroom", propertyDetail.NumberOfBedroom);
            var numberOfBathroom = new SqlParameter("@NumberOfBathroom", propertyDetail.NumberOfBathroom);
            var numberOfReceptionArea = new SqlParameter("@NumberOfReceptionArea", propertyDetail.NumberOfReceptionArea);
            var estPriceRangeLow = new SqlParameter("@EstPriceRangeLow", propertyDetail.EstPriceRangeLow);
            var estPriceRangeHigh = new SqlParameter("@EstPriceRangeHigh", propertyDetail.EstPriceRangeHigh);
            var sqMetres = new SqlParameter("@SqMetres", propertyDetail.SqMetres);
            var webURL = new SqlParameter("@WebURL", propertyDetail.WebUrl);

            using var context = new StageFiveContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"EXEC sp_AddAllProperty 
                     @UPRN
                    ,@Address
                    ,@HouseType
	                ,@Tenure
	                ,@NumberOfBedroom
	                ,@NumberOfBathroom 
	                ,@NumberOfReceptionArea
	                ,@EstPriceRangeLow
	                ,@EstPriceRangeHigh
                    ,@SqMetres
                    ,@WebURL";
                    //,@SoldDate
                    //,@SoldPrice";


                cmd.Parameters.Add(uprn);
                cmd.Parameters.Add(address);
                cmd.Parameters.Add(houseType);
                cmd.Parameters.Add(tenure);
                cmd.Parameters.Add(numberOfBedroom);
                cmd.Parameters.Add(numberOfBathroom);
                cmd.Parameters.Add(numberOfReceptionArea);
                cmd.Parameters.Add(estPriceRangeLow);
                cmd.Parameters.Add(estPriceRangeHigh);
                cmd.Parameters.Add(sqMetres);
                cmd.Parameters.Add(webURL);
                //cmd.Parameters.Add(soldDate);
                //cmd.Parameters.Add(soldPrice);


                var reader = cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                connection.Close();
            }

            return false;
        }


        //Getelementfor Sold
        private static void AddSoldPropertyDetails(Models.PropertyDetail propertyDetail)
        {
            if(propertyDetail.SoldData != null)
            {
                foreach (var p in propertyDetail.SoldData)
                {
                    var uprn = new SqlParameter("@UPRN", propertyDetail.UPRN);
                    var solddate = new SqlParameter("@SoldDate", p.SoldDate);
                    var soldprice = new SqlParameter("@SoldPrice", p.SoldPrice);


                    using var context = new StageFiveContext();
                    using var connection = context.Database.GetDbConnection();

                    try
                    {
                        if (connection.State == System.Data.ConnectionState.Closed)
                        {
                            connection.Open();
                        }

                        using var cmd = connection.CreateCommand();

                        cmd.CommandText = @"EXEC sp_AddSoldHistory 
                         @UPRN
                        ,@SoldDate
                        ,@SoldPrice";

                        cmd.Parameters.Add(uprn);
                        cmd.Parameters.Add(solddate);
                        cmd.Parameters.Add(soldprice);



                        var reader = cmd.ExecuteNonQuery();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
                    }
                    finally
                    {
                        connection.Close();
                    }

                }
            }           

        }












        static bool AddSoldHistory(Models.PropertyDetail propertyDetail)
        {
            var uprn = new SqlParameter("@UPRN", propertyDetail.UPRN);

            using var context = new StageFiveContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"EXEC sp_AddSoldHistory
                     @UPRN
                    ,@SoldDate                   
                    ,@SoldPrice";


                cmd.Parameters.Add(uprn);
                //cmd.Parameters.Add(soldDate);
                //cmd.Parameters.Add(soldPrice);


                var reader = cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                connection.Close();
            }

            return false;
        }










        static void StoreLog(string? sessionValue, string? logDetailsValue)
        {
            var moduleName = new SqlParameter("@Modulename", "Stage5");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new StageFiveContext();

            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_ProcessLogs @Modulename, @SessionID, @LogDetails";
                cmd.Parameters.Add(moduleName);
                cmd.Parameters.Add(sessionId);
                cmd.Parameters.Add(logDetails);

                var reader = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
                Environment.Exit(1);
            }
            finally
            {
                connection.Close();
            }
        }

        #endregion
    }
}
