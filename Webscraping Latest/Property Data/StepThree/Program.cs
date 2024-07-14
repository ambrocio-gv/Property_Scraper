using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;

namespace StepThree
{
    internal class Program
    {
        private static IConfiguration? _iconfiguration;

        private static bool runHeadless = true;
        static string dateTimeToday = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-S3-" + Environment.MachineName;

        static async Task Main(string[] args)
        {
            await SetConfiguration();


            StoreLog(dateTimeToday, "Start Program");

            var postcodes = GetPostcodes();
            var postCodeCount = postcodes.Count();

            StoreLog(dateTimeToday, $"Postcode count:{postCodeCount}");

            // Check if the user wants to disable headless mode
            if (args.Length > 0 && args[0].Equals("--showBrowser", StringComparison.OrdinalIgnoreCase))
            {
                runHeadless = false;
            }

            int counter = 1;

            foreach (var postcode in postcodes)
            {

                StoreLog(dateTimeToday, $"Checking Postcode : {counter}/{postCodeCount}, Postcode Used : {postcode}");

                var lids = await GetIds($"https://uprn.uk/postcode/{postcode.Value.Replace(" ", "")}");
                var countLids = lids.Count();

                if (lids.Count() <= 0)
                {
                    UpdatePostCodeUprn(postcode.Key, "Success");
                    StoreLog(dateTimeToday, $"UPRN Count: {countLids}, Postcode Used : {postcode}");
                }
                else
                {
                    UpdatePostCodeUprn(postcode.Key, "Success");
                    StoreLog(dateTimeToday, $"UPRN Count: {countLids}, Postcode Used : {postcode}");
                    AddUprn(lids, postcode.Value);
                }
                counter++;
            }

            StoreLog(dateTimeToday, "End Session");
            Environment.Exit(0);
        }


        static async Task<List<string>> GetIds(string url)
        {
            var list = new List<string>();

            await Task.Run(() =>
            {

                var chromeDriverService = ChromeDriverService.CreateDefaultService();

                var chromeOptions = new ChromeOptions();

                if (runHeadless)
                {
                    chromeOptions.AddArgument("--headless=new");
                }

                object path;
                path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null);

                if (path != null)
                    chromeOptions.BrowserVersion = FileVersionInfo.GetVersionInfo(path.ToString()).FileVersion;


                var driver = new ChromeDriver(chromeDriverService, chromeOptions);
                try
                {
                    driver.Navigate().GoToUrl(url);

                    var uls = driver.FindElement(By.XPath("/html/body/div[4]/div/div/ul"));
                    if (uls is not null)
                    {
                        var lis = uls.FindElements(By.TagName("li")).ToList();

                        foreach (var li in lis)
                        {
                            var innnerText = li.Text.Trim();
                            list.Add(innnerText);
                        }
                    }
                    driver.Quit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    driver.Quit();

                    var processes = Process.GetProcessesByName("chromedriver.exe");
                    foreach (Process process in processes)
                    {
                        process.Kill();
                    }

                }
            });
            return list;
        }

        static async Task<bool> SetConfiguration()
        {
            var jsonFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(jsonFile,
                optional: false, reloadOnChange: true);

            _iconfiguration = builder.Build();

            await Task.CompletedTask;
            return true;
        }


        static Dictionary<int, string> GetPostcodes()
        {

            var list = new Dictionary<int, string>();
            using var context = new StageThreeContext(_iconfiguration);
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_GetPostcode";
                var reader = cmd.ExecuteReader();


                while (reader.Read())
                {
                    list.Add(reader.GetInt32(0), reader.GetString(1));
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


        private static void UpdatePostCodeUprn(int id, string status)
        {
            using var context = new StageThreeContext(_iconfiguration);
            using var connection = context.Database.GetDbConnection();

            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                }

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "EXEC sp_UpdatePostcode_UPRN @ID, @UPRNProcessStatus";
                var idParam = new SqlParameter("@ID", id);
                var uprnProcessStatus = new SqlParameter("@UPRNProcessStatus", status);

                cmd.Parameters.Add(idParam);
                cmd.Parameters.Add(uprnProcessStatus);

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

        static void AddUprn(List<string> uprns, string postcode)
        {
            foreach (var uprn in uprns)
            {
                var idParam = new SqlParameter("@Postcode", postcode);
                var statusParam = new SqlParameter("@UPRN", uprn);

                using var context = new StageThreeContext(_iconfiguration);
                using var connection = context.Database.GetDbConnection();

                try
                {
                    if (connection.State == System.Data.ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    using var cmd = connection.CreateCommand();

                    cmd.CommandText = "EXEC sp_AddUPRN @Postcode, @UPRN";

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

        }



        static void StoreLog(string? sessionValue, string? logDetailsValue)
        {
            var moduleName = new SqlParameter("@Modulename", "Stage3");
            var sessionId = new SqlParameter("@SessionID", sessionValue);
            var logDetails = new SqlParameter("@LogDetails", logDetailsValue);


            using var context = new StageThreeContext(_iconfiguration);

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