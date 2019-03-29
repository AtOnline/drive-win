using Drive.Atonline.Rest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Drive.Atonline.Rest
{
    public class RestClient
    {

        public async static Task<T> Api<T>(string action)
        {
            return await Api<T>(action, "GET");
        }

        public async static Task<T> Api<T>(string action, string method)
        {
            return await Api<T>(action, method, new Dictionary<string, object>());
        }

        public async  static Task<T> Post<T>(string endpoint)
        {
            return await Post<T>(endpoint, new Dictionary<string, object>(), true);
        }

        public async static Task<T> Post<T>(string endpoint, Dictionary<string, object> values, bool json = true)
        {
            using (var httpClient = new HttpClient())
            {
                return await Post<T>(httpClient, endpoint, values, json);
            }
        }

        public async static Task<T> Post<T>(HttpClient httpClient, string endpoint, Dictionary<string, object> values, bool json = true)
        {
            using (HttpContent content = json ? new StringContent(values.ToJson(), Encoding.UTF8, "application/json") : (HttpContent)(new FormUrlEncodedContent(values.ToDictionary(k => k.Key, k => k.Value.ToString()))))
            {
                return await InternalPost<T>(httpClient, endpoint, content);
            }
        }

        private async static Task<T> InternalPost<T>(HttpClient httpClient, string endpoint, HttpContent content)
        {
            return await InternalRequest<T>(httpClient, HttpMethod.Post, endpoint, content);
        }

        private async static Task<T> InternalDelete<T>(HttpClient httpClient, string endpoint, HttpContent content)
        {
            return await InternalRequest<T>(httpClient, HttpMethod.Delete, endpoint, content);
        }

        private async static Task<T> InternalRequest<T>(HttpClient httpClient, HttpMethod method, string endpoint, HttpContent content)
        {
            if (method == HttpMethod.Get) throw new InvalidOperationException("To Execute a GET request use InternalGet instead");

            using (var message = new HttpRequestMessage(method, endpoint) { Content = content })
            using (var reponse = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead))
           /* {
               var r = await reponse.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(r);

            }*/
            using (Stream s = await reponse.Content.ReadAsStreamAsync())
            using (StreamReader sr = new StreamReader(s))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new JsonSerializer();

                return serializer.Deserialize<T>(reader);
            }
        }

        private async static Task<T> InternalGet<T>(HttpClient httpClient, string endpoint, Dictionary<string, string> p)
        {
            using (var content = new FormUrlEncodedContent(p)) {
                var builder = new UriBuilder(endpoint);
                builder.Query = await content.ReadAsStringAsync();

                using (Stream s = await httpClient.GetStreamAsync(new Uri(builder.ToString())))
                using (StreamReader sr = new StreamReader(s))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new JsonSerializer();

                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        public async static Task<T> Api<T>(string action, string method, Dictionary<string, object> values)
        {
            using (var httpClient = new HttpClient())
            {
                var tkn = await getBearerToken();

                if (tkn != "")
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tkn);

                switch (method)
                {
                    case "POST":
                        var v = values.ToJson();
                        using (var content = new StringContent(v, Encoding.UTF8, "application/json"))
                        {
                            return await InternalPost<T>(httpClient, Config.API_ENDPOINT + action, content);
                        }
                    case "DELETE":
                        using (var content = new StringContent(values.ToJson(), Encoding.UTF8, "application/json"))
                        {
                            return await InternalDelete<T>(httpClient, Config.API_ENDPOINT + action, content);
                        }
                    case "GET":
                        return await InternalGet<T>(httpClient, Config.API_ENDPOINT + action, values.ToDictionary(k => k.Key, k => k.Value.ToString()));
                    default:
                        throw new NotSupportedException($"RestClient.Api Method {method} not supported");

                }
            }
        }

        private static async Task<string> getBearerToken()
        {
            if (Config.ApiToken == null) return "";
            if (Config.ApiToken.isExpired())
            {
                if (!await refreshToken())
                {
                    throw new Exception();
                }
            }

            return Config.ApiToken.Token;
        }

        public async static Task<bool> refreshToken()
        {
            var p = new Dictionary<string, object>()
                {
                    { "grant_type","refresh_token" },
                    { "client_id",Config.CLIENT_ID },
                    { "refresh_token",Config.ApiToken.RefreshToken},
                };

            var r = await Post<Dictionary<string, object>>(Config.TOKEN_ENDPOINT, p, true);

            Config.ApiToken = new BaererToken((string)r["access_token"], Config.ApiToken.RefreshToken, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)r["expires_in"]);

            return true;
        }

        public async static Task<List<Drive>> Drives()
        {
            return await retrieveAll<Drive>("Drive");
        }

        public async static Task<List<T>> retrieveAll<T>(string action, string method) where T : new()
        {
            return await retrieveAll<T>(action, method, new Dictionary<string, object>());
        }

        public async static Task<List<T>> retrieveAll<T>(string action) where T : new()
        {
            return await retrieveAll<T>(action, "GET");
         }

        public async static Task<List<T>> retrieveAll<T>(string action, string method, Dictionary<string, object> values) where T : new()
        {
            List<T> rsl = new List<T>();
            int page = 1;
            Dictionary<string, object> p = new Dictionary<string, object>();
            if (values == null) values = new Dictionary<string, object>();
            p.Add("results_per_page", "1000");
            p.Add("page", page.ToString());
            p = p.Union(values).ToDictionary(k => k.Key, v => v.Value);
            int lastPage = int.MaxValue;

            do
            {
                p["page"] = page.ToString();
                var d = await Api<RestResponse<List<T>>>(action, method, p);
                rsl.AddRange(d.data);
                page++;
                lastPage = d.paging.page_max;
            } while (page < lastPage);

            return rsl;
        }

    }

    static class Extension
    {
        public static string ToJson(this Dictionary<string, object> dictionary)
        {
            var kvs = dictionary.Select(kvp =>
            {
                if (kvp.Value is string) return string.Format("\"{0}\":\"{1}\"", HttpUtility.JavaScriptStringEncode(kvp.Key), HttpUtility.JavaScriptStringEncode((string)kvp.Value));
                if (kvp.Value is bool) return string.Format("\"{0}\":{1}", kvp.Key, kvp.Value.ToString().ToLower());
                return  string.Format("\"{0}\":{1}", kvp.Key, kvp.Value);
            }
            );
            return string.Concat("{", string.Join(",", kvs), "}");
        }

        public static bool IsNumber(this object value)
        {
            return value is sbyte
                    || value is byte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
        }
    }
}



