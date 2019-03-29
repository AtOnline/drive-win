using DokanNet;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline
{
    
    public class FSNode
    {
        public FSNode(DriveItem item, string dir)
        {
            Item = item;
            Dir = dir;
            Path = System.IO.Path.Combine(dir, item.Name);
        }

        public FSNode(DriveItem item)
        {
            Item = item;
            Dir = null;
            Path = "\\";
        }

   
        public FSNodeStream getStream()
        {
            return new FSNodeStream(this);
        }

        public DriveItem Item { get; private set; }

        public void SetItem(DriveItem item)
        {
            Item = item;
            FetchTime = DateTime.UtcNow;
            _fileAttributes = null;
            _fileInformation = null;

        }


        public string Dir { get; }
        public string Path { get; }
        public DateTime FetchTime { get;private set; } = DateTime.UtcNow;

        private FileAttributes? _fileAttributes = null;
        public FileAttributes FileAttributes
        {
            get
            {
                if (_fileAttributes == null)
                {
                    _fileAttributes = FileAttributes.Normal;
                    if (IsDirectory) _fileAttributes |= FileAttributes.Directory;
                    if (IsSpecial) _fileAttributes |= FileAttributes.Offline | FileAttributes.Hidden;

                }

                return (FileAttributes)_fileAttributes;
            }
        }


        private FileInformation? _fileInformation = null;
        public FileInformation FileInfo
        {
            get
            {
                if (_fileInformation == null)
                {
                    _fileInformation = new FileInformation()
                    {
                        Attributes = FileAttributes,
                        CreationTime = Item.Created?.UtcDateTime,
                        LastAccessTime = Item.Last_Modified?.UtcDateTime,
                        LastWriteTime = Item.Last_Modified?.UtcDateTime,
                        Length = string.IsNullOrEmpty(Item.Size) ? 0 : long.Parse(Item.Size),
                        FileName = Item.Name
                    };
                }

                return (FileInformation)_fileInformation;
            }
        }

        public bool IsExpired(int expirationSeconds) => DateTime.UtcNow > FetchTime.AddSeconds(expirationSeconds);

        public bool IsDirectory => Item.Type == "folder";
        public bool IsSpecial => Item.Type == "special";
    }
}

