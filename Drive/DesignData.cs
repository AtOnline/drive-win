using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drive
{
    public class DesignData
    {        public List<Drive.Atonline.Rest.Drive> AvailableDrives
        {
            get
            {
                return new List<Drive.Atonline.Rest.Drive>() {
                new Drive.Atonline.Rest.Drive()
                {
                    Name = "My Drive 1 Hello Hillo Pouet",
                    Usage = "-%",
                    Usage_Float = 0f,
                    Plan = "unlimited",
                    Quota_fmt = "inf",
                    Root = new Atonline.Rest.DriveItem(){Size_fmt="118.15 GB"}
                },
                new Drive.Atonline.Rest.Drive()
                {
                    Name = "My Drive 2",
                    Plan = "free",
                    Usage = "22%",
                    Usage_Float = 0.22f,
                    Quota_fmt="1024.00 MB",
                    Root = new Atonline.Rest.DriveItem(){Size_fmt="220.15 MB"}
                },
                 new Drive.Atonline.Rest.Drive()
                {
                    Name = "My Drive 3",
                    Plan = "free",
                    Usage = "80%",
                    Usage_Float = 0.80f,
                    Quota_fmt="1024.00 MB",
                    Root = new Atonline.Rest.DriveItem(){Size_fmt="800.15 MB"}
                },
                  new Drive.Atonline.Rest.Drive()
                {
                    Name = "My Drive 4",
                    Plan = "free",
                    Usage = "100%",
                    Usage_Float = 1f,
                    Quota_fmt="1024.00 MB",
                    Root = new Atonline.Rest.DriveItem(){Size_fmt="1024 MB"}
                },
                new Drive.Atonline.Rest.Drive()
                {
                    Name = "My Drive 5",
                    Plan = "free",
                    Usage = "100%",
                    Usage_Float = 1f,
                    Quota_fmt="1024.00 MB",
                    Root = new Atonline.Rest.DriveItem(){Size_fmt="1024 MB"}
                }
            };
            }
            set { }
        }
    }
}
