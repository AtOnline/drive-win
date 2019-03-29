using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline.Rest
{
    public class Drive
    {
        public string Catalog_Product__ { get; set; }
        public RestDate Created { get; set; }
        public string Drive__ { get; set; }
        public RestDate Expires { get; set; }
        public string Free { get; set; }
        public string Free_fmt { get; set; }
        public RestDate Last_Modified { get; set; }
        public string Name { get; set; }
        public string Plan { get; set; }
        public string Quota_fmt { get; set; }
        public string Usage { get; set; }
        public string User_Billing__ { get; set; }
        public float Usage_Float { get; set; }
        public long? Quota { get; set; }
        public DriveItem Root { get; set; }
    }
}
