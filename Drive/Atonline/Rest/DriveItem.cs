using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline.Rest
{
    public class DriveItem
    {
        public string Container { get; set; }
        public string Drive_Item__ { get; set; }
        public string Drive__ { get; set; }
        public string Filename { get; set; }
        public string Icon { get; set; }
        public string Key_Name { get; set; }
        public string Name { get; set; }
        public string Parent_Drive_Item__ { get; set; }
        public string Mime { get; set; }
        public string Size { get; set; }
        private long? _sizeLong = null;
        public long SizeLong
        {
            get
            {
                if (_sizeLong == null)
                    _sizeLong = long.Parse(Size);
                return (long)_sizeLong;
            }
            set
            {
                _sizeLong = value;
                Size = value.ToString();
            }
        }
        public string Size_fmt { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string Download_Url { get; set; }
        public RestDate Indexed { get; set; }
        public RestDate Last_Modified { get; set; }
        public RestDate Created { get; set; }
        public RestDate Deleted { get; set; }
    }
}
