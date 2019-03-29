using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Xml;
using System.Net;
using DokanNet;

namespace Drive.Atonline
{
    public class Uploader : IDisposable
    {
        const int PARRALLEL_UPLOAD_COUNT = 3;
        public FSNode Node { get; }
        const string emptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        Upload _uploadInfo = null;
        MemoryStream _ms;
        byte[] _buffer;
        object _bufferLock = new object();
        int _blockSize = 5242880;
        int _partNumber = 0;
        SortedDictionary<int, string> etags = new SortedDictionary<int, string>();
        string _awsUrl;
        bool _blockPending = false;
        int _currentByteWritten = 0;
        long _totalByteWritten = 0;
        bool _disposed = false;
        long? expectedOffset = null;
        List<Task> _blockTasks = new List<Task>();

        public Uploader(FSNode node)
        {
            Node = node;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Flush();
            lock (_bufferLock)
            {
                if (_ms != null)
                {
                    _ms.Dispose();
                    _ms.Close();
                    _buffer = null;
                }
            }
        }

        private void touch()
        {
            lock (_bufferLock)
            {
                if (Node.Item.Drive_Item__ != null) return;
                var param = new Dictionary<string, object>() { { "filename", System.IO.Path.GetFileName(Node.Path) } };
                Node.SetItem(RestClient.Api<RestResponse<DriveItem>>($"Drive/Item/{Node.Item.Parent_Drive_Item__}:touch", "POST", param).Result.data);
            }
        }

        public void initialize(long offset)
        {
            _buffer = new byte[_blockSize];
            _ms = new MemoryStream(_buffer);

            if (Node.Item.Drive_Item__ == null)
                touch();
            var p = new Dictionary<string, object>() { {"offset", offset } };
            var resp = RestClient.Api<RestResponse<Upload>>($"Drive/Item/{Node.Item.Drive_Item__}:overwrite", "POST", p).Result;
            _uploadInfo = resp.data;
            
            StringBuilder b = new StringBuilder();
            b.AppendFormat("https://{0}/{1}/{2}", _uploadInfo.Bucket_Endpoint.Host, _uploadInfo.Bucket_Endpoint.Name, _uploadInfo.Key);
            _awsUrl = b.ToString();

            using (var initMessage = new HttpRequestMessage(HttpMethod.Post, _awsUrl + "?uploads="))
            {
                initMessage.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream"); // Change this and send the right one
                initMessage.Headers.Add("X-Amz-Acl", "private");
                var r = SignAndDo(initMessage);
                XmlDocument doc = new XmlDocument();
                var xml = r.Content.ReadAsStringAsync().Result;
                doc.LoadXml(xml);
                _uploadInfo.UploadID = doc.GetElementsByTagName("UploadId")[0].InnerText;
                r.Dispose();
            }
            expectedOffset = offset;
        }

        public void Write(byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            lock (_bufferLock)
            {
                if (_uploadInfo == null)
                    initialize(offset);

                if(offset != expectedOffset)
                {
                    // We tried to write on another part of the file, so we need to complete the current upload and start a new block
                    Flush();
                    initialize(offset);
                }

                var remaningSpaceInBlock = _blockSize - _currentByteWritten;
                var bytesToWrite = buffer.Length;
                if (buffer.Length > remaningSpaceInBlock) bytesToWrite = remaningSpaceInBlock;


                _ms.Position = _currentByteWritten;
                _ms.Write(buffer, 0, bytesToWrite);
                _blockPending = true;
                _currentByteWritten += bytesToWrite;
                _totalByteWritten += bytesToWrite;
                bytesWritten = bytesToWrite;
                expectedOffset += bytesToWrite;

                if (_currentByteWritten >= _blockSize)
                {
                    UploadBlock();
                    info.TryResetTimeout(600000);
                }
            }
        }

        private void UploadBlock()
        {
            lock (_bufferLock)
            {
                _ms.Flush();


                var uploadMessage = new HttpRequestMessage(HttpMethod.Put, _awsUrl + $"?partNumber={_partNumber + 1}&uploadId={_uploadInfo.UploadID}");

                   byte[] tmp = new byte[_currentByteWritten];
                   Buffer.BlockCopy(_ms.ToArray(), 0, tmp,0, _currentByteWritten);
                    uploadMessage.Content = new ByteArrayContent(tmp);

                    Action<int,HttpRequestMessage> t = (partN, message) =>
                    {
                            using (var r = SignAndDo(uploadMessage))
                            {
                                etags.Add(partN+1,r.Headers.ETag.ToString());
                               
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                GC.Collect();
                            }

                        message.Content.Dispose();
                        message.Dispose();
                    };

                var tmpPartN = _partNumber;
                _blockTasks.Add(Task.Run(() => { t(tmpPartN, uploadMessage); }));

                _ms.SetLength(0);
                _ms.Dispose();
                _ms.Close();
                _buffer = null;
                _buffer = new byte[_blockSize];
                _partNumber += 1;
                _currentByteWritten = 0;
                _blockPending = false;

                _ms = new MemoryStream(_buffer);
                _ms.SetLength(0);

                if (_blockTasks.Count >= PARRALLEL_UPLOAD_COUNT)
                {
                    WaitUploadCompleted();
                }
            }
        }

        private void WaitUploadCompleted()
        {
            Task.WaitAll(_blockTasks.ToArray());
            _blockTasks.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public void Flush()
        {
            if (Node.Item.Drive_Item__ == null)
                touch();

            lock (_bufferLock)
            {
                if (_uploadInfo == null) return;

                if (_blockPending)
                {
                    UploadBlock();
                }

                WaitUploadCompleted();

                using (var complete = new HttpRequestMessage(HttpMethod.Post, _awsUrl + $"?uploadId={_uploadInfo.UploadID}"))
                {

                    StringBuilder xml = new StringBuilder();
                    xml.Append("<CompleteMultipartUpload>");
                    foreach(var kv in etags)
                    {
                        xml.Append("<Part><PartNumber>");
                        xml.Append((kv.Key).ToString());
                        xml.Append("</PartNumber><ETag>");
                        xml.Append(kv.Value);
                        xml.Append("</ETag></Part>");
                    }
                    for (int i = 0; i < etags.Count; i++)
                    {
                       
                    }

                    xml.Append("</CompleteMultipartUpload>");

                    using (var c = new StringContent(xml.ToString()))
                    {
                        complete.Content = c;
                        using (var r = SignAndDo(complete)) { }
                    }

                    Node.SetItem(Rest.RestClient.Api<Rest.RestResponse<DriveItem>>($"Cloud/Aws/Bucket/Upload/{_uploadInfo.Cloud_Aws_Bucket_Upload__}:handleComplete", "POST").Result.data);

                    etags.Clear();
                    _currentByteWritten = 0;
                    _totalByteWritten = 0;
                    _partNumber = 0;
                    _uploadInfo = null;
                    expectedOffset = null;
                }
            }
        }
        
        private HttpResponseMessage SignAndDo(HttpRequestMessage httpMessage)
        {
            var bodyHash = emptyHash;

            if (httpMessage.Content != null)
            {
                using (var s = httpMessage.Content.ReadAsStreamAsync().Result)
                {
                    if (s.Length > 0)
                    {
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            bodyHash = ByteConverter.ByteArrayToHex(sha256.ComputeHash(s));

                        }
                    }
                }
            }

            var ts = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var tsD = ts.Substring(0, 8);

            httpMessage.Headers.Add("X-Amz-Content-Sha256", bodyHash);
            httpMessage.Headers.Add("X-Amz-Date", ts);

            var awsAuthStr = new List<string>(){
                "AWS4-HMAC-SHA256",
                    ts,
                    tsD + "/" + _uploadInfo.Bucket_Endpoint.Region + "/s3/aws4_request",
                    httpMessage.Method.ToString(),
                    httpMessage.RequestUri.AbsolutePath,
                    httpMessage.RequestUri.Query.TrimStart('?'),
                    "host:" + httpMessage.RequestUri.Host,
               };


            var signHead = new List<string>() { "host" };
            var sortedArray = new List<string>();

            foreach (var h in httpMessage.Headers)
            {
                sortedArray.Add(h.Key);
            }

            sortedArray.Sort();

            foreach (var h in sortedArray)
            {
                var s = h.ToLower();
                if (!s.StartsWith("x-")) continue;
                signHead.Add(s);
                awsAuthStr.Add($"{s}:{httpMessage.Headers.GetValues(h).First()}");
            }

            awsAuthStr.Add("");
            awsAuthStr.Add(String.Join(";", signHead));
            awsAuthStr.Add(bodyHash);
            var toSign = String.Join("\n", awsAuthStr);
            var r = Rest.RestClient.Api<Rest.RestResponse<Dictionary<string, string>>>($"Cloud/Aws/Bucket/Upload/{_uploadInfo.Cloud_Aws_Bucket_Upload__}:signV4", "POST", new Dictionary<string, object>() { { "headers", toSign } }).Result;

            if (r == null || r.data == null) return null;
            if (!r.data.ContainsKey("authorization")) return null;

            httpMessage.Headers.TryAddWithoutValidation("Authorization", r.data["authorization"]);


            using (var client = new HttpClient())
            {
                return client.SendAsync(httpMessage).Result;
            }
        }

        public void Truncate(long length)
        {
            Flush();
            lock (_bufferLock)
            {
                var param = new Dictionary<string, object>() { { "size", length } };
                var toto = RestClient.Api<RestResponse<DriveItem>>($"Drive/Item/{Node.Item.Drive_Item__}:truncate", "POST", param).Result;
                Node.SetItem(toto.data);
            }
        }
    }
}