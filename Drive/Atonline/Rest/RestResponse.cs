using DokanNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline.Rest
{
    public class RestDate
    {
        public string full { get; set; }
        public string iso { get; set; }
        public string tz { get; set; }
        public string us { get; set; }
        public long unix { get; set; }

        public DateTime UtcDateTime
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime; }
        }
    }

    public class RestPaging
    {
        public string count { get; set; }
        public int page_max { get; set; }
        public int page_no { get; set; }
        public int results_per_page { get; set; }
    }

    public class RestAccess
    {
        public string available { get; set; }
        public RestDate expires { get; set; }
        public string required { get; set; }
        public string user_group { get; set; }
    }

    public class RestResponse<T>
    {
        public Dictionary<string, RestAccess> access { get; set; }
        public string result { get; set; }
        public double time { get; set; }
        public RestPaging paging { get; set; }
        public T data { get; set; }
    }
}
