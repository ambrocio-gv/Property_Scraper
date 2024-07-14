using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace StageFour
{
    public class Program
    {
        private static bool runHeadless = true;
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S4-" + Environment.MachineName;
        static By SelectorByAttributeValue(string p_strAttributeName, string p_strAttributeValue)
        {
            return (By.XPath(String.Format("//*[@{0} = '{1}']",
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
            // testing if it can read the appsettings chromedriver.
            // Console.WriteLine(AppSettingsJsonParser.GetChromeDriverFolder());
            // return;

            StoreLog(dateTimeToday, "Start Program");
            StoreLog(dateTimeToday, "Getting Proxies and UPRNs");

            var uprns = GetUprns();

            if (uprns is null) return;
            try
            {
                var chromeDriverService = ChromeDriverService.CreateDefaultService();

                object path;
                path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

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

                    int i = 0;
                    foreach (var proxy in proxies)
                    {
                        i++;
                        var chromeOption = GetChromeOptions(proxy.ProxyIP, proxy.Port);                  

                        if (path != null)
                            chromeOption.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;

                        if (runHeadless)
                        {
                            chromeOption.AddArgument("--headless=new");
                        }

                        StoreLog(dateTimeToday, $"Checking UPRN:{uprn}, Proxy IP:{proxy.ProxyIP}");
                        Console.WriteLine($"Checking UPRN:{uprn}, Proxy IP:{proxy.ProxyIP} @{DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt")}");

                        var driver = GetChromeDriver(chromeDriverService, chromeOption);
                        var Been2OfficeIP = 0; 
                        OfficeIP:
                        var url = $"https://www.zoopla.co.uk/property/uprn/{uprn}/";
                        var proxyResult = GetProxyResult(driver, url);

                        try
                        {
                            if (proxyResult == "NoRespond" || proxyResult == "Blocked" || proxyResult == "Cloudflare")
                            {

                                driver.Quit();
                                UpdateUprn(uprn, "Not Respond");
                                UpdateProxyIP(proxy.Id, proxyResult);

                            }
                            else
                            {
                                UpdateProxyIP(proxy.Id, "Success");

                                var filter = "//*[@id='main-content']/div[1]/div/div/div/div/div/div/div";
                                var oprnStatus = driver.FindElements(By.XPath(filter)).ToList();
                                if (oprnStatus is null || oprnStatus.Count == 0)
                                {
                                    UpdateUprn(uprn, "Not Found");
                                    driver.Quit();
                                    break;
                                }

                                var text = oprnStatus.FirstOrDefault()?.Text.Trim().ToUpper();

                                if (text is not null)
                                {
                                    UpdateUprn(uprn, text);

                                    if (string.IsNullOrEmpty(text) || text == "CURRENTLY OFF-MARKET")
                                    {
                                        driver.Quit();
                                        break;
                                    }
                                    else
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

                                        AddProperty(scrapeElement.AddressElement,
                                            scrapeElement.BedElement,
                                            scrapeElement.BathElement,
                                            scrapeElement.ChairElement,
                                            scrapeElement.FloorAreaElement,
                                            scrapeElement.Elements.FirstOrDefault(),
                                            lowPrice,
                                            highPrice,
                                            scrapeElement.SoldDateElement,
                                            scrapeElement.SoldPriceElement,
                                            uprn,
                                            url);

                                        driver.Quit();
                                    }
                                }
                                else
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
                        if ((i % 2) == 0 && Been2OfficeIP == 0)
                        {
                            chromeOption = GetLocalChromeOptions();
                            driver = GetChromeDriver(chromeDriverService, chromeOption);

                            if (path != null)
                                chromeOption.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;

                            proxy.ProxyIP = "OfficeIP";
                            Been2OfficeIP = 1;
                            goto OfficeIP;
                        }


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

        //Getelementfor Sold
        static IWebElement? GetElementFromTimeline(List<IWebElement> properiesElements)
        {
            if (properiesElements != null)
            {
                var pTags = properiesElements.FirstOrDefault()?.FindElements(By.XPath("//section[@id='timeline']/ul/li/following-sibling::div/p"));

                if (pTags != null)
                {
                    return pTags.FirstOrDefault();
                }
            }
            return null;
        }

        private static Models.ScrapeElement GetScrapedData(ChromeDriver driver)
        {
            var scrapeElement = new Models.ScrapeElement();
            try
            {
                var addressFilter = "//*[@id='main-content']/div[2]/div/div/div[1]/div/section[1]/div[1]/h1";
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

                var soldDate = driver.FindElement(By.XPath("//section[@data-testid='timeline']//div[contains(., 'Sold')]/following-sibling::p[1] | //section[@data-testid='timeline']//div[contains(., 'Sold')]/following-sibling::div/p"));
                var soldPrice = driver.FindElement(By.XPath("//section[@data-testid='timeline']//div[div[contains(., 'Sold')]]/div/p[1]"));

                scrapeElement.AddressElement = addressElement;
                scrapeElement.FloorAreaElement = floorAreaElement;
                scrapeElement.BedElement = bedElement;
                scrapeElement.BathElement = bathElement;
                scrapeElement.ChairElement = chairElement;
                scrapeElement.Elements = elements;
                scrapeElement.HighPriceItem = highPrice;
                scrapeElement.LowPriceItem = lowPrice;
                scrapeElement.SoldDateElement = soldDate;
                scrapeElement.SoldPriceElement = soldPrice;

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
            IWebElement? soldDate,
            IWebElement? soldPrice,
            string uprn,
            string webUrl)
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

                propertyDetail.SoldDate = "";
                propertyDetail.SoldPrice = "";

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

                if (soldDate is not null)
                { 
                    propertyDetail.SoldDate = soldDate.Text.ToString();
                }

                if (soldPrice is not null)
                {
                    propertyDetail.SoldPrice = soldPrice.Text.ToString();
                }


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


                }

                AddPropertyDetails(propertyDetail);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static bool UpdateUprn(string uprnValue, string textValue)
        {
            var uprn = new SqlParameter("@UPRN", uprnValue);
            var listingStatus = new SqlParameter("@ListingStatus", textValue);

            using var context = new StageFourContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdateUPRN @UPRN, @ListingStatus";
                cmd.Parameters.Add(uprn);
                cmd.Parameters.Add(listingStatus);

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
        static async Task<bool> SetConfiguration()
        {
            try
            {
                var jsonFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

                var builder = new ConfigurationBuilder()
                    .SetBasePath(Environment.CurrentDirectory)
                    .AddJsonFile(jsonFile,
                    optional: false, reloadOnChange: true);

                IConfiguration _iconfiguration = builder.Build();

                await Task.CompletedTask;
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n\n" + ex.StackTrace);
                return false;
            }
        }
        static List<string> GetUprns()
        {
            var list = new List<string>();
            using var context = new StageFourContext();
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
            using var context = new StageFourContext();
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

                //int i = 1;
                while (reader.Read())
                {
                    list.Add(new Models.Proxy
                    {
                        Id = reader.GetInt32(0),
                        ProxyIP = reader.GetString(1),
                        Port = reader.GetString(2)
                    });

                    //added to increase proxy count if adding local IP every 2 counts
                    //if ((i % 2) == 0)
                    //{
                    //    list.Add(new Models.Proxy { Id = 0, ProxyIP = "192.168.10.5", Port = "" });
                    //}
                    //i++;
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
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(5);

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

            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);

            return chromeOptions;

        }
        static ChromeOptions GetLocalChromeOptions()
        {

            var chromeOptions = new ChromeOptions();
            //chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;

            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);

            return chromeOptions;

        }

        static void UpdateProxyIP(int? id, string? status)
        {
            var idParam = new SqlParameter("@ID", id);
            var statusParam = new SqlParameter("@Status", status);

            using var context = new StageFourContext();
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

                var filter = "//*[@id='main-content']/div[2]/div/div/div[1]/div/section[1]/div[1]/h1";

                var errorCode = driver.FindElements(By.ClassName("error-code")).Count;
                if (errorCode != 0) return "NoRespond";

                var Notfound = driver.FindElements(By.XPath("//*[@id='main-content']/div/section/div/div[2]/div/div[2]/p[1]")).Count();
                //*[@id="main-content"]/div/section/div/div[2]/div/div[2]/p[1]
                if (Notfound != 0) return "NotFound";

                var headerAddress = driver.FindElements(By.XPath(filter)).Count;
                if (headerAddress != 0) return "Success";

                var cloudflare = driver.FindElements(By.XPath("//a[text()='Cloudflare']")).Count();
                if (cloudflare != 0) return "Cloudflare";

                var forbiddenAddress = driver.FindElements(By.XPath("//h1[text()='403 Forbidden']")).Count;
                var accessDenied = driver.FindElements(By.XPath("//h1[text()='Access Denied']")).Count;

                if (forbiddenAddress != 0 || accessDenied != 0) return "Blocked";

                else return "NoRespond";
            }
            catch
            {
                return "NoRespond";
            }
            return string.Empty;
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
            var webURL = new SqlParameter("@WebURL", propertyDetail.WebUrl);
            var soldDate = new SqlParameter("@SoldDate", propertyDetail.SoldDate);
            var soldPrice = new SqlParameter("@SoldPrice", propertyDetail.SoldPrice);

            using var context = new StageFourContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"EXEC sp_AddProperty 
                     @UPRN
                    ,@Address
                    ,@HouseType
	                ,@Tenure
	                ,@NumberOfBedroom
	                ,@NumberOfBathroom 
	                ,@NumberOfReceptionArea
	                ,@EstPriceRangeLow
	                ,@EstPriceRangeHigh
                    ,@WebURL
                    ,@SoldDate
                    ,@SoldPrice";


                cmd.Parameters.Add(uprn);
                cmd.Parameters.Add(address);
                cmd.Parameters.Add(houseType);
                cmd.Parameters.Add(tenure);
                cmd.Parameters.Add(numberOfBedroom);
                cmd.Parameters.Add(numberOfBathroom);
                cmd.Parameters.Add(numberOfReceptionArea);
                cmd.Parameters.Add(estPriceRangeLow);
                cmd.Parameters.Add(estPriceRangeHigh);
                cmd.Parameters.Add(webURL);
                cmd.Parameters.Add(soldDate);
                cmd.Parameters.Add(soldPrice);


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
            var moduleName = new SqlParameter("@Modulename", "Stage4");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new StageFourContext();

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
