using DokanNet;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline
{
    public class FSNodeStream : IDisposable
    {
        public FSNodeStream(FSNode item)
        {
            Item = item;
            _uploader = new Uploader(Item);
        }

        private long _pos = 0;
        public Stream _fs;
        readonly object locker = new object();
        public FSNode Item { get; }

        public bool Writing { get; set; }
        private bool disposing = false;
        private Uploader _uploader;

        public NtStatus Read(byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            lock (locker)
            {
                Flush();

                if (_fs != null)
                {
                    _fs.Close();
                    _fs.Dispose();
                    _fs = null;
                }

                if (_fs != null && _fs.CanRead)
                {
                    if (offset > _pos && (offset) < (_pos + 8 * 1024))
                    {
                        var drop = offset - _pos;
                        var dropBytes = new byte[drop];
                        var rb = _ = Read(dropBytes);
                        _pos += rb;
                    }

                    if (offset == _pos)
                    {
                        bytesRead = Read(buffer);
                        _pos += bytesRead;

                        return NtStatus.Success;
                    }

                    _fs.Close();
                    _fs.Dispose();
                    _fs = null;
                }

                if (offset < 0)
                {
                    bytesRead = 0;
                    return NtStatus.Error;
                }

                if (offset >= Item.Item.SizeLong)
                {
                    bytesRead = 0;
                    return NtStatus.Error;
                }

                if (Item.Item.Download_Url == null || Item.Item.Download_Url == "")
                {
                    bytesRead = 0;
                    return NtStatus.Error;
                }

                using (var httpClient = new HttpClient())
                {
                    if (offset != 0)
                    {
                        httpClient.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, null);
                    }

                    _pos = offset;

                    _fs = httpClient.GetStreamAsync(Item.Item.Download_Url).Result;

                    bytesRead = Read(buffer);
                    if (bytesRead > 0)
                    {
                        _pos += bytesRead;
                    }

                    return NtStatus.Success;
                }

            }

        }

        private int Read(byte[] data)
        {
            if (_fs == null || !_fs.CanRead) return 0;
            int totalRead = 0;
            int br = 0;

            while ((br = _fs.Read(data, totalRead, data.Length - totalRead)) > 0)
            {
                totalRead += br;
            }

            return totalRead;
        }

        public void Flush()
        {
            _uploader.Flush();
        }

        public void Write(byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            _uploader.Write(buffer, out bytesWritten, offset, info);
        }

        public void Dispose()
        {
            throw new Exception("You should use cleanup function instead");
        }

        private void Dispose(bool deleteOnClose)
        {
            if (disposing) return;
            disposing = true;
            if(!deleteOnClose) // The file will be deleted when closing, no need to write the changes on the servers
                Flush();

            lock (locker)
            {
                if (_fs != null)
                {
                    _fs.Close();
                    _fs.Dispose();
                    _fs = null;
                }

            }
        }

        public void Close(bool deleteOnClose = false)
        {
            Dispose(deleteOnClose);
        }

        public void Cleanup(bool deleteOnClose = false)
        {
            Dispose(deleteOnClose);
        }

        ~FSNodeStream()
        {
            Dispose(false);
        }

        public void SetLength(long length)
        {
            _uploader.Truncate(length);
        }

        public void Append(byte[] buffer, out int bytesWritten, DokanFileInfo info)
        {
            _uploader.Write(buffer, out bytesWritten, Item.Item.SizeLong, info);
        }
    }
}
