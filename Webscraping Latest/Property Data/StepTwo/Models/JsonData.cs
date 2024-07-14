namespace StepTwo.Models
{
    public class AppSettingsJsonParser
    {
        public static string? GetConnectionString()
        {
            var fileExists = AppDomain.CurrentDomain.BaseDirectory + "appsettings.json";
            if (!File.Exists(fileExists)) return String.Empty;

            var newtonSoft = File.ReadAllText(fileExists);

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonData>(newtonSoft);

            if (result is not null)
            {
                var connectionString = result.ConnectionStrings?.DefaultConnection;
                return connectionString;
            }

            return string.Empty;
        }
    }

    public class JsonData
    {
        public ConnectionStrings? ConnectionStrings { get; set; }
    }

    public class ConnectionStrings
    {
        public string? DefaultConnection { get; set; } = string.Empty;
    }
}
