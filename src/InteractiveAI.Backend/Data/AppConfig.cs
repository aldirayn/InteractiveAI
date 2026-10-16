using System.Reflection;

namespace InteractiveAI.Backend.Data
{
    public static class AppConfig
    {
        private static IConfigurationRoot _configuration;
        public static IConfigurationRoot Configuration
        {
            get
            {
                if (_configuration != null)
                    return _configuration;


                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var builder = new ConfigurationBuilder().SetBasePath(basePath).AddJsonFile("appsettings.json");
                _configuration = builder.Build();
                return _configuration;
            }
        }

        public static IConfigurationRoot GetCurrentDeirectory(string directory)
        {
            if (_configuration != null)
                return _configuration;

            var builder = new ConfigurationBuilder().SetBasePath(directory).AddJsonFile("appsettings.json");
            _configuration = builder.Build();
            return _configuration;
        }

        public static AppConfiguration Config
        {
            get
            {
                return new AppConfiguration();
            }
        }

        public class AppConfiguration
        {
            public AppConfiguration()
            {

                string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                   .SetBasePath(basePath)
                   .AddJsonFile("appsettings.json")
                   .Build();
                GetConfig(configurationRoot);
            }

            #region Get Configuration
            private void GetConfig(IConfiguration configuration)
            {
                Environment = configuration.GetSection("Environment").Value;
                CookieExpiration = Convert.ToInt32(configuration.GetSection("CookieExpiration").Value);
                ConfigDev = configuration.GetSection("Dev").Get<Cfg_Dev>();
                ConfigProd = configuration.GetSection("PROD").Get<Cfg_Prod>();
                ConfigAPI = configuration.GetSection("API").Get<Cfg_API>();
                ConfigJwt = configuration.GetSection("Jwt").Get<Cfg_Jwt>();
                ConfigEmail = configuration.GetSection("Email").Get<Cfg_Email>();
                ConfigFile = configuration.GetSection("FileStorage").Get<Cfg_FileStorage>();
                ConfigNLog = configuration.GetSection("NLog").Get<Cfg_NLog>();
            }

            public string Environment { get; set; }
            public int CookieExpiration { get; set; }
            public Cfg_Dev ConfigDev { get; set; }
            public Cfg_Prod ConfigProd { get; set; }
            public Cfg_Jwt ConfigJwt { get; set; }
            public Cfg_API ConfigAPI { get; set; }
            public Cfg_Email ConfigEmail { get; set; }
            public Cfg_FileStorage ConfigFile { get; set; }
            public Cfg_NLog ConfigNLog { get; set; }

            public class Cfg_Dev
            {
                public string Mysql { get; set; }
                public string MkaPool { get; set; }
                public string Kagt { get; set; }
            }
            public class Cfg_Prod
            {
                public string connection { get; set; }
                public string KagtConnection { get; set; }
            }
            public class Cfg_Jwt
            {
                public string Username { get; set; }
                public string Password { get; set; }
                public string Key { get; set; }
                public string Issuer { get; set; }
                public string Audience { get; set; }
                public int ExpiryMinutes { get; set; }
            }
            public class Cfg_BaseAPI
            {
                public string BaseUrl { get; set; }
                public string Endpoint { get; set; }
            }
            public class Cfg_API
            {
                public Cfg_APIAuth Auth { get; set; }
                public Cfg_APIUser User { get; set; }
                public Cfg_APICourse Course { get; set; }
            }
            public class Cfg_APIAuth
            {
                public string BaseUrl { get; set; }
                public Cfg_BaseAPI GenerateKey { get; set; }
                public Cfg_BaseAPI Token { get; set; }
            }
            public class Cfg_APIUser
            {
                public string BaseUrl { get; set; }
                public Cfg_BaseAPI List { get; set; }
                public Cfg_BaseAPI Submit { get; set; }
            }
            public class Cfg_APICourse
            {
                public string BaseUrl { get; set; }
                public Cfg_BaseAPI List { get; set; }
                public Cfg_BaseAPI ListCustomer { get; set; }
                public Cfg_BaseAPI Submit { get; set; }
                public Cfg_BaseAPI UploadCustomer { get; set; }
                public Cfg_BaseAPI UploadStudent { get; set; }
                public Cfg_BaseAPI DownloadReportCourse { get; set; }
                public Cfg_BaseAPI DownloadReportCourseStream { get; set; }
            }

            #region Config Email
            public class Cfg_Email
            {
                public string To { get; set; }
                public string CC { get; set; }
                public string Subject { get; set; }
                public Cfg_SMTP SMTPConfigurations { get; set; }
            }
            public class Cfg_SMTP
            {
                public string Host { get; set; }
                public int Port { get; set; }
                public string Username { get; set; }
                public string Password { get; set; }
                public bool SSL { get; set; }
                public bool IsAuthentication { get; set; }
            }
            #endregion

            #region Config File
            public class Cfg_FileStorage
            {
                public string Folder { get; set; }
                public string Path { get; set; }
                public string Username { get; set; }
                public string Password { get; set; }
            }
            #endregion Config File

            #region Config NLog
            public class Cfg_NLog
            {
                public string LogDir { get; set; } = @"C:\Logs\API\KAGT\PWO";
                public string InternalLogFile { get; set; } = @"C:\temp\nlog-internal.log";
            }
            #endregion Config NLog

            #endregion Get Configuration

        }
    }
}
