using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline
{
    public class BaererToken
    {
        public BaererToken(string token, string refreshToken, long expiresAt)
        {
            Token = token;
            ExpiresAt = expiresAt;
            RefreshToken = refreshToken;
        }

        public string Token { get; }
        public string RefreshToken { get; }
        public long ExpiresAt { get; }

        public bool isExpired()
        {
            return ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
    class Config
    {
        public const string HUB_ENDPOINT = "https://hub.atonline.com/";

        public const string LOGIN_RESPONSE_TYPE = "code";
        public const string LOGIN_SCOPE = "Drive+profile";
        public const string CLIENT_ID = "oaap-bqyhk7-y2g5-dcvk-l5pe-7p7vj3u4";

        public const string REDIRECT_HOST = "drive.atonline.com";
        public const string REDIRECT_ACTION = "/app-login";
        public const string REDIRECT_URI = "https://"+ REDIRECT_HOST+ REDIRECT_ACTION;


        public const string AUTH_ENDPOINT = "https://hub.atonline.com/_special/rest/OAuth2:auth";
        public const string TOKEN_ENDPOINT = "https://hub.atonline.com/_special/rest/OAuth2:token";
        public const string API_ENDPOINT = "https://hub.atonline.com/_special/rest/";

        private static BaererToken _apiToken;
        public static BaererToken ApiToken
        {
            get {

                if (_apiToken != null) return _apiToken;
                if (Settings.Default.Token == "") return _apiToken;

                _apiToken = new BaererToken(
                    Unprotect(Settings.Default.Token),
                    Unprotect(Settings.Default.RefreshToken),
                     Settings.Default.Expiration
                    );


                return _apiToken;

            } set
            {
                _apiToken = value;
                Settings.Default.Token = _apiToken == null ? "" : Protect(_apiToken.Token);
                Settings.Default.Expiration = _apiToken == null ? 0 : _apiToken.ExpiresAt;
                Settings.Default.RefreshToken = _apiToken == null ? "" : Protect(_apiToken.RefreshToken);
                Settings.Default.Save();
            }
        }

        public static string LoginUrl { get
            {
               return AUTH_ENDPOINT +
                    "?embed=1&response_type=" + System.Web.HttpUtility.UrlEncode(LOGIN_RESPONSE_TYPE) +
                    "&scope=" + System.Web.HttpUtility.UrlEncode(LOGIN_SCOPE) +
                    "&client_id=" + System.Web.HttpUtility.UrlEncode(CLIENT_ID) +
                    "&redirect_uri=" + System.Web.HttpUtility.UrlEncode(REDIRECT_URI);
            }
        }


        public static string Protect(string str)
        {
            byte[] entropy = Encoding.ASCII.GetBytes(Assembly.GetExecutingAssembly().FullName);
            byte[] data = Encoding.ASCII.GetBytes(str);
            string protectedData = Convert.ToBase64String(ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser));
            return protectedData;
        }

        public static string Unprotect(string str)
        {
            byte[] protectedData = Convert.FromBase64String(str);
            byte[] entropy = Encoding.ASCII.GetBytes(Assembly.GetExecutingAssembly().FullName);
            string data = Encoding.ASCII.GetString(ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser));
            return data;
        }

    }
}
