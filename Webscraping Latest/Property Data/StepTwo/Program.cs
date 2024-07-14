using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using System.Linq;

namespace StepTwo
{
    internal class Program
    {
        private static bool runHeadless = true;
        public static string LogFile = AppDomain.CurrentDomain.BaseDirectory + @"stage-two-results.log";
        public static string LogFile2 = AppDomain.CurrentDomain.BaseDirectory + @"stage-two-results-postcode.log";
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S2a-" + Environment.MachineName;
        static By SelectorByAttributeValue(string p_strAttributeName, string p_strAttributeValue)
        {
            return (By.XPath(String.Format("//*[@{0} = '{1}']",
                                           p_strAttributeName,
                                           p_strAttributeValue)));
        }

        static ChromeDriver GetChromeDriver(ChromeDriverService chromeDriverService, ChromeOptions chromeOptions)
        {
            try
            {
                var driver = new ChromeDriver(chromeDriverService, chromeOptions, TimeSpan.FromMinutes(10));
                driver.Manage().Window.Maximize();
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(30);
                //driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(10);
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(2);

                return driver;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new ChromeDriver(chromeDriverService, chromeOptions);
            }
          
        }
        static bool CheckCannotBeReached(ChromeDriver driver, int? id,ChromeDriverService chromeDriverService)
        {

            //This site can’t be reached and This page isn’t working

            //var filter = "span[contains(text(),'This site can’t be reached')]";
            var cannotBeReached = driver.FindElements(By.XPath("//span[text()='This site can’t be reached']"));
            if (cannotBeReached.Count() >= 1)
            {
                driver.Quit();
                driver.Dispose();
                chromeDriverService.Dispose();
                UpdateProxyIP(id, "NoRespond");
                return true;
            }

            return false;
        }
        static bool CheckPageNotWorking(ChromeDriver driver, int? id,ChromeDriverService chromeDriverService)
        {

            var pageNotWorking = driver.FindElements(By.XPath("//span[text()='This page isn’t working']"));

            if (pageNotWorking.Count() >= 1)
            {
                driver.Quit();
                driver.Dispose();
                chromeDriverService.Dispose();
                UpdateProxyIP(id, "NoRespond");
                return true;
            }

            return false;
        }

        static bool CheckRayId(ChromeDriver driver, int? id,ChromeDriverService chromeDriverService)
        {
            //cloudflare
            var hasRayId = driver.FindElements(By.XPath("//div[contains(text(),'Ray ID:')]"));
            if (hasRayId.Any())
            {
                driver.Quit();
                driver.Dispose();
                chromeDriverService.Dispose();
                UpdateProxyIP(id, "NoRespond");
                return true;
            }

            return false;
        }




        static void Main(string[] args)
        {

            StoreLog(dateTimeToday, "Start Program");
            StoreLog(dateTimeToday, "Getting Proxies and Postal Outwards");

            var postalOutwards = GetPostOutward();
            var baseUrl = "https://www.zoopla.co.uk/for-sale/property/";

            int startPostO = 1;

            int i = 0;

            foreach (var postalOutward in postalOutwards)
            {
                var proxies = GetProxies();

                // Check if the user wants to disable headless mode
                if (args.Length > 0 && args[0].Equals("--showBrowser", StringComparison.OrdinalIgnoreCase))
                {
                    runHeadless = false;
                }

                if (proxies.Count == 0) break;

                using var chromeDriverService = ChromeDriverService.CreateDefaultService();
                var chromeOption = new ChromeOptions();


                try
                {
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

                        if (runHeadless)
                        {
                            chromeOption.AddArgument("--headless=new");
                        }

     

                        object path;
                        path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

                        if (path != null)
                            chromeOption.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;


                        var driver = GetChromeDriver(chromeDriverService, chromeOption);          

                        var postcodeText = $"Checking PostO:{postalOutward.Posto} " +
                            $"({startPostO}/{postalOutwards.Count}), Ip Used: {proxy.ProxyIP}:{proxy.Port}";
                        StoreLog(dateTimeToday, postcodeText);


                        var url = $"{baseUrl}{postalOutward.Posto}/?q={postalOutward.Posto}&chain_free=true&added=7_days";

                        //var url = $"https://www.zoopla.co.uk/for-sale/property/br3/?q=br3&chain_free=true&added=7_days";

                        try
                        {
                           var check = url;

                            driver.Navigate().GoToUrl(url);

                            if (CheckCannotBeReached(driver, proxy.Id, chromeDriverService)) continue;
                            if (CheckPageNotWorking(driver, proxy.Id, chromeDriverService)) continue;
                            if (CheckRayId(driver, proxy.Id, chromeDriverService)) continue;


                            var isNotFound = driver
                                .FindElements(By.XPath("//h1[contains(text(),'Sorry, we could not find a place name matching')]"));

                            if (isNotFound.Count() >= 1)
                            {
                                driver.Quit();
                                driver.Dispose();
                                chromeDriverService.Dispose();

                                var processes = Process.GetProcessesByName("chromedriver.exe");
                                foreach (Process process in processes)
                                {
                                    process.Kill();
                                }
                                //make a void process to kill

                                StoreLog(dateTimeToday, $"PostO Result:{postalOutward.Posto}, Status:Not Found");
                                UpdatePostalOutward(postalOutward.Posto, "NotFound");
                                UpdateProxyIP(proxy.Id, "Success");
                                startPostO++;
                                break;
                            }
                            else
                            {
                                var searchResults2 = driver.FindElements(SelectorByAttributeValue("data-testid", "total-results"));

                                if (searchResults2.Count() == 0)
                                {
                                    driver.Quit();
                                    UpdateProxyIP(proxy.Id, "NoRespond");
                                    continue;
                                }

                                var searchResults = searchResults2.FirstOrDefault();

                                if (searchResults is not null)
                                {
                                    if (searchResults.Text.Trim() != "")
                                    {
                                        var postalOutwardResult = searchResults.Text.Trim();

                                        driver.Quit();
                                        driver.Dispose();
                                        chromeDriverService.Dispose();

                                        UpdatePostalOutward(postalOutward.Posto, postalOutwardResult);
                                        UpdateProxyIP(proxy.Id, "Success");
                                        var text = $"Result of Postcode:{postalOutwardResult}, Postcode used: {postalOutward.Posto}";
                                        StoreLog(dateTimeToday, text);

                                        break;
                                    }
                                }
                            }


                            driver.Close();
                        }
                        catch (Exception ex)
                        {
                            StoreLog(dateTimeToday, "INNER EXECPTION -- " + ex.Message + "\n\n" + ex.StackTrace);
                            UpdateProxyIP(proxy.Id, "NoRespond");
                            driver.Quit();
                            var processes = Process.GetProcessesByName("chromedriver.exe");
                            foreach (Process process in processes)
                            {
                                process.Kill();
                            }

                            continue;
                        }

                        driver.Quit();


                    }

                    startPostO++;
                }
                catch (Exception ex)
                {
                    StoreLog(dateTimeToday, ex.Message + "\n\n" + ex.StackTrace);

                }

            }


            StoreLog(dateTimeToday, "End Program");
            Environment.Exit(0);
        }



        static ChromeOptions GetChromeOptions(string? ipAddress, string? port)
        {
            var chromeOptions = new ChromeOptions();

            chromeOptions.AddArguments(
                "--proxy-server=" + ipAddress + ":" + port
                ,"no-sandbox"
                ,"--disable-software-rasterizer"
                , "--disable-gpu");

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

            chromeOptions.AddArguments(
                 "no-sandbox"
                , "--disable-software-rasterizer"
                , "--disable-gpu");

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

        static List<Models.PostalOutward> GetPostOutward()
        {
            var list = new List<Models.PostalOutward>();
            using var context = new StepTwoContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_GetPostO";
                var reader = cmd.ExecuteReader();


                while (reader.Read())
                {
                    list.Add(new Models.PostalOutward { Posto = reader.GetString(0) });
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
            using var context = new StepTwoContext();
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
                        list.Add(new Models.Proxy{Id=0, ProxyIP="", Port=""});
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
        static void UpdatePostalOutward(string? postalOutward, string result)
        {
            var postO = new SqlParameter("@Posto", postalOutward);
            var searchResult = new SqlParameter("@SearchResult", result);

            using var context = new StepTwoContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdatePostO @Posto, @SearchResult";
                cmd.Parameters.Add(postO);
                cmd.Parameters.Add(searchResult);

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
        static void FindAddress(ChromeDriver driver, List<Models.ThoroughfareModel> thoroughfares)
        {
            try
            {
                StoreLog(dateTimeToday, "Finding Addresses");

                int i = 1;
                int count = thoroughfares.Count;

                foreach (var thoroughfare in thoroughfares)
                {
                    StoreLog(dateTimeToday, $"Checking Thoroughfare: {i} out of {count}");
                    if (thoroughfare is not null)
                    {
                        var pCode = thoroughfare.PostCode;
                        var mailUrl = "https://www.zoopla.co.uk/for-sale/property";
                        var filterUrl = "&results_sort=newest_listings&search_source=for-sale&chain_free=&added=7_days";
                        var secondUrl = $"{mailUrl}/{thoroughfare.Town?.Trim()}/{thoroughfare.Thoroughfare?.Trim()}/{pCode}/?q={pCode}{filterUrl}";

                        driver.Navigate().GoToUrl(secondUrl);

                        var xPath = "//*[@id='main-content']/div/div[4]/div[2]/section/div[1]/div/div[1]/p";
                        var secondResultElement = driver.FindElements(By.XPath(xPath)).ToList();
                        var secondResult = secondResultElement.FirstOrDefault();

                        if (secondResultElement is null || secondResult is null) continue;

                        var secondResultText = secondResult.Text.Trim();

                        UpdateThoroghfareCheck(thoroughfare.ID, secondResultText);
                    }
                    i++;
                }
            }
            catch (Exception ex)
            {
                StoreLog(dateTimeToday, ex.Message + "\n\n" + ex.StackTrace);
            }
            finally
            {
                driver.Quit();
            }



        }
        static string GetProxyResult(ChromeDriver driver)
        {
            try
            {
                driver.Navigate().GoToUrl("https://www.zoopla.co.uk/property/uprn/10070707660/");

                var filter = "//h1[text()= '2 Angel Close, Hampton Hill, Hampton, TW12 1RG']";
                var errorCode = driver.FindElements(By.ClassName("error-code")).Count();
                var headerAddress = driver.FindElements(By.XPath(filter)).Count();
                var forbiddenAddress = driver.FindElements(By.XPath("//h1[text()='403 Forbidden']")).Count();

                if (errorCode != 0) return "NoRespond";

                if (headerAddress != 0) return "Success";

                if (forbiddenAddress != 0) return "Blocked";

            }
            catch
            {
                driver.Quit();
                return "NoRespond";
            }
            finally
            {
                driver.Quit();
            }

            return string.Empty;
        }

        static void UpdateProxyIP(int? id, string? status)
        {
            var idParam = new SqlParameter("@ID", id);
            var statusParam = new SqlParameter("@Status", status);

            using var context = new StepTwoContext();
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

        static List<Models.ThoroughfareModel> GetThoroughfare(string? postalOutward)
        {
            var posto = new SqlParameter("@Posto", postalOutward);

            var list = new List<Models.ThoroughfareModel>();
            using var context = new StepTwoContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_GetTFare @Posto";
                cmd.Parameters.Add(posto);
                var reader = cmd.ExecuteReader();


                while (reader.Read())
                {
                    var model = new Models.ThoroughfareModel
                    {
                        ID = reader.GetInt32(0),
                        PostCode = reader.GetString(1),
                        Town = reader.GetString(2),
                        Thoroughfare = reader.GetString(3)
                    };

                    list.Add(model);
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

        static void UpdateThoroghfareCheck(int? idValue, string? checkResultValue)
        {
            var id = new SqlParameter("@ID", idValue);
            var checkResult = new SqlParameter("@CheckResult", checkResultValue);


            using var context = new StepTwoContext();
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdateTFare_Check @ID, @CheckResult";
                cmd.Parameters.Add(id);
                cmd.Parameters.Add(checkResult);

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
            var moduleName = new SqlParameter("@Modulename", "Stage2");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new StepTwoContext();

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