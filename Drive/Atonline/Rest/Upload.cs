using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline.Rest
{
    public class BuketEndpoint
    {
        public string Host { get; set; }
        public string Name { get; set; }
        public string Region { get; set; }
    }

    public class Upload
    {
        public string Blob__ { get; set; }
        public BuketEndpoint Bucket_Endpoint { get; set; }
        public string Cloud_Aws_Bucket_Upload__ { get; set; }
        public string Cloud_Aws_Bucket__ { get; set; }
        public string Complete { get; set; }
        public string Key { get; set; }
        public string UploadID { get; set; }
        public string PUT { get; set; }
        public float? Size { get; set; }
        public string Status { get; set; }

    }
}
