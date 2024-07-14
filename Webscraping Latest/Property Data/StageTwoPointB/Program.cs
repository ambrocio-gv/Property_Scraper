
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;

namespace StageTwoPointB
{

    public class Program
    {
        #region Functions

        private static bool runHeadless = true;
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S2b-" + Environment.MachineName;


        static ChromeDriver GetChromeDriver(ChromeDriverService chromeDriverService, ChromeOptions chromeOptions)
        {
            var driver = new ChromeDriver(chromeDriverService, chromeOptions);
            var Timestamp = new DateTimeOffset(DateTime.UtcNow);
            driver.Manage().Window.Maximize();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); //ask gerard
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
            return driver;
        }
        static ChromeOptions GetChromeOptions(string? ipAddress, string? port)
        {

            var chromeOptions = new ChromeOptions();

            //chromeOptions.AddArgument("--proxy-server=" + ipAddress + ":" + port); //change
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
            chromeOptions.AddArgument("no-sandbox");

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

        static List<Models.Proxy> GetProxies()
        {
            var list = new List<Models.Proxy>();
            using var context = new StageTwoPointBContext();
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
                    //    list.Add(new Models.Proxy { Id = 0, ProxyIP = "", Port = "" });
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
        
        static void UpdateProxyIP(int? id, string? status)
        {
            var idParam = new SqlParameter("@ID", id);
            var statusParam = new SqlParameter("@Status", status);

            using var context = new StageTwoPointBContext();
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

        static List<Models.Postcode> GetPostCodes()
        {
            var list = new List<Models.Postcode>();

            using var context = new StageTwoPointBContext();
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
                    list.Add( new Models.Postcode { 
                        ID = reader.GetInt32(0),
                        PostCode = reader.GetString(1)
                    });
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

        static void UpdatePostcodeSearch(int? id,string? searchResult)
        {
            var idParam = new SqlParameter("@ID", id);
            var statusParam = new SqlParameter("@SearchResult", searchResult);

            using var context = new StageTwoPointBContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdatePostcode_Search @ID, @SearchResult";

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


        static void StoreLog(string? sessionValue, string? logDetailsValue)
        {
            var moduleName = new SqlParameter("@Modulename", "Stage2.B");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new StageTwoPointBContext();

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

        static By SelectorByAttributeValue(string p_strAttributeName, string p_strAttributeValue)
        {
            return (By.XPath(String.Format("//*[@{0} = '{1}']",
                                           p_strAttributeName,
                                           p_strAttributeValue)));
        }

        #endregion  

        static void Main(string[] args)
        {
            
            StoreLog(dateTimeToday, "Start Program");
            StoreLog(dateTimeToday, "Getting Proxies and Postal Outwards");

            var postcodes = GetPostCodes();
            int postcodeCount = postcodes.Count();

            var baseUrl = "https://www.zoopla.co.uk/for-sale/property/";
            int postcodeStart = 1;

            // Check if the user wants to disable headless mode
            if (args.Length > 0 && args[0].Equals("--showBrowser", StringComparison.OrdinalIgnoreCase))
            {
                runHeadless = false;
            }

            int i = 0;  

            foreach (var postcode in postcodes)
            {
                var proxies = GetProxies();
                if (proxies.Count == 0) break;

                var chromeDriverService = ChromeDriverService.CreateDefaultService();
                var chromeOption = new ChromeOptions();

              

                foreach (var proxy in proxies)
                {
                    //Mix in IP every 3rd after 2 proxy IPs
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

                    if (runHeadless)
                    {
                        //chromeOption.AddArgument("--headless=new");
                    }

                    var postcodeText = $"Checking Postcodes: {postcodeStart}/{postcodeCount}, Ip Used: {proxy.ProxyIP}:{proxy.Port}";

                    var url = $"{baseUrl}{postcode.PostCode}/?q={postcode.PostCode}&chain_free=&added=7_days";

                    object path;
                    path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

                    if (path != null)
                        chromeOption.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;


                    var driver = new ChromeDriver(chromeDriverService, chromeOption, TimeSpan.FromMinutes(10));
                    driver.Manage().Window.Maximize();
                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(5);

                    StoreLog(dateTimeToday, postcodeText);
                    try
                    {
                        driver.Navigate().GoToUrl(url);

                        var searchResults = driver.FindElement(SelectorByAttributeValue("data-testid", "total-results"));

                        if (searchResults is null)
                        {
                            UpdatePostcodeSearch(postcode.ID, "0 results");
                            driver.Quit();
                            continue;
                        }
                        if (searchResults.Text.Trim() != "")
                        {
                            UpdatePostcodeSearch(postcode.ID, searchResults.Text.Trim());
                            UpdateProxyIP(proxy.Id, "Success");
                            var text = $"Result of Postcode:{searchResults.Text.Trim()}, Postcode used: {postcode.PostCode}";
                            StoreLog(dateTimeToday, text);
                            driver.Quit();
                            break;
                        }

                        driver.Close();
                        driver.Quit();
                        driver.Dispose();
                    }
                    catch (Exception ex)
                    {
                        StoreLog(dateTimeToday, ex.Message + "\n\n" + ex.StackTrace);
                        UpdateProxyIP(proxy.Id, "NoRespond");
                        driver.Quit();

                        var processes = Process.GetProcessesByName("chromedriver.exe");
                        foreach (Process process in processes)
                        {
                            process.Kill();
                        }
                    }
                    
                }

                postcodeStart++;
            }
           
        

            StoreLog(dateTimeToday, "End Program");
            Environment.Exit(0);
        }



    }

  
}




