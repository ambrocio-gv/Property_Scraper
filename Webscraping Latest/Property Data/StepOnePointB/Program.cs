using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using SeleniumExtras.WaitHelpers;

namespace StageOne
{

    internal class Program
    {
        private static bool runHeadless = true;
        public static string LogFile = AppDomain.CurrentDomain.BaseDirectory + @"proxy.log";
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S1-" + Environment.MachineName;

        static async Task Main(string[] args)
        {
            StoreLog(dateTimeToday, "Start Program");

            // Check if the user wants to disable headless mode
            if (args.Length > 0 && args[0].Equals("--showBrowser", StringComparison.OrdinalIgnoreCase))
            {
                runHeadless = false;
            }

            //await TaskUSHttps();


            await TaskAll();


            Thread.Sleep(5000);
            StoreLog(dateTimeToday, "End-Session");
            Environment.Exit(0);

            await Task.CompletedTask;

        }

        private static async Task<string> CallUrl(string fullUrl)
        {
            HttpClient client = new();
            var response = await client.GetStringAsync(fullUrl);
            return response;
        }

        static async Task MainTask(string proxysite, string country)
        {
            StoreLog(dateTimeToday, "Collect the proxies in the link: " + proxysite);


            //var result = await CallUrl(proxysite);  

            #region for IP Proxies

            var chromeDriverServiceProxy = ChromeDriverService.CreateDefaultService();

            ChromeOptions chromeOptionsProxy = GetChromeOptionsProxySite();

            object pathProxy;
            pathProxy = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

            if (pathProxy != null)
                chromeOptionsProxy.BrowserVersion = FileVersionInfo.GetVersionInfo(pathProxy.ToString()).FileVersion;

            chromeOptionsProxy.AddArgument("--headless=new");
            var driverProxy = new ChromeDriver(chromeDriverServiceProxy, chromeOptionsProxy);

            var proxies = new ProxyRepository().GetProxies(proxysite, driverProxy, country);



            var proxyCount = proxies.Count();

            StoreLog(dateTimeToday, $"Number of proxy collected:{proxyCount}");

            #endregion

            #region Storing IP Proxies


            int i = 1;


            foreach (var item in proxies)
            {
                StoreLog(dateTimeToday, $"Checking proxy: {i}/{proxyCount}, Proxy Used: {item.IpAddress}:{item.Port}, Country: {item.Country} ");

                var chromeDriverService = ChromeDriverService.CreateDefaultService();

                ChromeOptions chromeOptions = GetChromeOptions(item.IpAddress, item.Port);

                object path;
                path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

                if (path != null)
                    chromeOptions.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;

                if (runHeadless)
                {
                    chromeOptions.AddArgument("--headless=new");
                }

                var driver = new ChromeDriver(chromeDriverService, chromeOptions);
                var startPageLoad = DateTime.Now;

                driver.Manage().Window.Maximize();
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);

                var proxyResult = "";
                try
                {
                    //var url = $"https://www.zoopla.co.uk/property/uprn/10006573197/";
                    var url = $"https://www.zoopla.co.uk/";



                    driver.Navigate().GoToUrl(url);

                    var endPageLoad = DateTime.Now;
                    var loadTime = endPageLoad - startPageLoad;
                    var loadTimeSeconds = loadTime.TotalSeconds.ToString();

                    proxyResult = GetProxyResult(driver);

                    if (proxyResult == "NoRespond")
                    {
                        StoreLog(dateTimeToday, $"{proxyResult} - {item.IpAddress}:{item.Port}" + "\n\n" + loadTimeSeconds);
                        

                    }
                    else if (proxyResult == "Success")
                    {
                        StoreLog(dateTimeToday, $"{proxyResult} - {item.IpAddress}:{item.Port}" + "\n\n" + loadTimeSeconds);
                    }

                    else if (proxyResult == "Cloudflare")
                    {
                        StoreLog(dateTimeToday, $"{proxyResult} - {item.IpAddress}:{item.Port}" + "\n\n" + loadTimeSeconds);


                    }

                    else if (proxyResult == "Blocked")
                    {
                        StoreLog(dateTimeToday, $"{proxyResult} - {item.IpAddress}:{item.Port}" + "\n\n" + loadTimeSeconds);


                    }

                    if (proxyResult.Equals("Success"))
                    {
                        StoreLog(dateTimeToday, "Saving to the database success result");
                        StoreDatabase(item.IpAddress, item.Port, loadTimeSeconds, item.Country);
                    }

                    driver.Close();
                    driver.Quit();
                    driver.Dispose();
                }
                catch (Exception ex)
                {

                    proxyResult = "NoRespond";
                    StoreLog(dateTimeToday, $"{proxyResult} - {item.IpAddress}:{item.Port}" + "\n\n" + ex.Message);
                    driver.Quit();

                    var processes = Process.GetProcessesByName("chromedriver.exe");
                    foreach (Process process in processes)
                    {
                        process.Kill();
                    }

                }

                i++;
            }
            #endregion


            await Task.CompletedTask;
        }



        static async Task TaskUSHttps()
        {            
            

            //await MainTask("http://free-proxy.cz/en/proxylist/country/US/https/ping/all", "United States - Https");
        }


        static async Task TaskAll()
        {


            await MainTask("http://free-proxy.cz/en/proxylist/country/all/https/ping/all", "All - Https");


        }




        static string GetProxyResult(ChromeDriver driver)
         {
            try
            {
                //var filter = "//h1[text()= '2 Angel Close, Hampton Hill, Hampton, TW12 1RG']";
                //var filter = "*//h1[text()= '11 Patrick Stirling Court, Doncaster, DN4 0EU']";
                var filter = "*//p[text()= 'We know what a home is really worth']";


                var errorCode = driver.FindElements(By.ClassName("error-code")).Count;
                if (errorCode != 0) return "Error Code";

                var headerAddress = driver.FindElements(By.XPath(filter)).Count;
                if (headerAddress != 0) return "Success";
                

                var cloudflare = driver.FindElements(By.XPath("//a[text()='Cloudflare']")).Count;
                //WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
                //wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//*[@id='challenge-stage']/div/label/input")));
                //IWebElement cloudflarecheckbox = driver.FindElement(By.XPath("//*[@id='challenge-stage']/div/label/input"));
                //cloudflarecheckbox.Click();

                //wait.Until(ExpectedConditions.ElementIsVisible(By.XPath(filter)));


                if (cloudflare != 0) return "Cloudflare";





                var forbiddenAddress = driver.FindElements(By.XPath("//h1[text()='403 Forbidden']")).Count;
                var accessDenied = driver.FindElements(By.XPath("//h1[text()='Access Denied']")).Count;
                
                if (forbiddenAddress != 0 || accessDenied != 0) return "Blocked";
            }
            catch
            {
                return "NoRespond";
            }
            return "NoRespond";
         }

        static void StoreDatabase(string? ipAddress, string? portValue, string? loadTimeValue, string? countryValue)
        {
            var ip = new SqlParameter("@ProxyIP", ipAddress);
            var port = new SqlParameter("@Port", portValue);
            var loadTime = new SqlParameter("@LoadTime", loadTimeValue);
            var country = new SqlParameter("@Country", countryValue);


            using var context = new ProxyContext();

            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.CommandText = "sp_AddProxyIP_v1_2";
                cmd.Parameters.Add(ip);
                cmd.Parameters.Add(port);
                cmd.Parameters.Add(loadTime);
                cmd.Parameters.Add(country);

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

        static string GetProxyResult(ChromeDriver driver, Models.Proxy item)
        {
            return String.Empty;
        }

        public static ChromeOptions GetChromeOptions(string? ipAddress, string? port)
        {
            new DriverManager().SetUpDriver(new ChromeConfig());

            var chromeOptions = new ChromeOptions();
            //chromeOptions.AddArgument("--silent-launch");
            //chromeOptions.AddArgument("--ignore-gpu-blocklist");

            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;

            chromeOptions.AddArguments("--proxy-server=" + ipAddress + ":" + port,"no-sandbox");
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
            chromeOptions.AddArgument("ignore-ssl-errors=yes");
            chromeOptions.AddArgument("ignore-certificate-errors");
            chromeOptions.AddArgument("allow-running-insecure-content");
            //chromeOptions.AddArgument("--auto-open-devtools-for-tabs");

            //chromeOptions.AddLocalStatePreference("browser", new { enabled_labs_experiments = new string[] {"dns_over_https.mode@secure", "dns_over_https.templates@https://dns.google/dns-query{?dns}"} });



            chromeOptions.AddArgument("--user-agent=\"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\"");


            return chromeOptions;

        }

        public static ChromeOptions GetChromeOptionsProxySite()
        {
            new DriverManager().SetUpDriver(new ChromeConfig());

            var chromeOptions = new ChromeOptions();
            //chromeOptions.AddArgument("--silent-launch");
            //chromeOptions.AddArgument("--ignore-gpu-blocklist");

            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;

            //chromeOptions.AddArguments("--proxy-server=" + ipAddress + ":" + port, "no-sandbox");
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddUserProfilePreference("intl.accept_languages", "nl");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
            chromeOptions.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
            chromeOptions.AddArgument("ignore-ssl-errors=yes");
            chromeOptions.AddArgument("ignore-certificate-errors");
            chromeOptions.AddArgument("allow-running-insecure-content");
            //chromeOptions.AddArgument("--auto-open-devtools-for-tabs");

            //chromeOptions.AddLocalStatePreference("browser", new { enabled_labs_experiments = new string[] {"dns_over_https.mode@secure", "dns_over_https.templates@https://dns.google/dns-query{?dns}"} });



            chromeOptions.AddArgument("--user-agent=\"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\"");


            return chromeOptions;

        }





        static ChromeDriver GetChromeDriver(ChromeDriverService chromeDriverService, ChromeOptions chromeOptions)
        {
            var driver = new ChromeDriver(chromeDriverService, chromeOptions);
            driver.Manage().Window.Maximize();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);

            Console.Clear();
            return driver;
        }

        static async Task<List<Models.Proxies>> GetSuccessfulIpAddress(string logFile)
        {
            var list = new List<Models.Proxies>();
            var lines = File.ReadAllLines(logFile);
            foreach (var item in lines)
            {
                var status = item.Split('-').FirstOrDefault()?.Trim();
                var splittedItem = item.Split('-').LastOrDefault();
                if (status == "Success")
                {
                    if (splittedItem is not null)
                    {
                        var ip = splittedItem.Split(':').FirstOrDefault();
                        var port = splittedItem.Split(':').LastOrDefault();

                        var proxy = new Models.Proxies
                        {
                            ProxyIP = ip,
                            Port = port,
                            DateAdded = DateTime.UtcNow,
                            Status = status


                        };
                        list.Add(proxy);
                    }
                }
            }
            await Task.CompletedTask;
            return list;
        }


        static void StoreLog(string? sessionValue, string? logDetailsValue)
        {
            var moduleName = new SqlParameter("@Modulename", "Stage1");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new ProxyContext();

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
            }
            finally
            {
                connection.Close();
            }
        }

    }
}