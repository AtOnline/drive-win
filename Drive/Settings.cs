using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drive
{
    internal sealed partial class Settings
    {
        public static Dictionary<string, char> ExtractSettingDriveInfos()
        {
            var drives = new Dictionary<string, char>();
            foreach (var pair in Default.Drives.Split('|'))
            {
                var infos = pair.Split(':');
                if (infos.Length < 2) continue;
                drives.Add(infos[1], infos[0][0]);
            }

            return drives;
        }
    }
}
