using DokanNet;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DFileAccess = DokanNet.FileAccess;


namespace Drive.Atonline
{
    public class VirtualDrive : IDokanOperations
    {

        private const DFileAccess DataAccess = DFileAccess.ReadData | DFileAccess.WriteData | DFileAccess.AppendData |
                              DFileAccess.Execute |
                              DFileAccess.GenericExecute | DFileAccess.GenericWrite | DFileAccess.GenericRead;

        private const DFileAccess DataReadAccess = DFileAccess.ReadData | DFileAccess.GenericExecute |
                                                   DFileAccess.Execute;

        private const DFileAccess DataWriteAccess = DFileAccess.WriteData | DFileAccess.AppendData |
                                                   DFileAccess.Delete |
                                                   DFileAccess.GenericWrite;


        private Thread _th = null;
        public char DriveLetter { get; set; }
        public object Files { get; }

        public DriveProvider Provider;

        public VirtualDrive(char driveLetter, Rest.Drive data)
        {
            DriveLetter = driveLetter;
            Provider = new DriveProvider(data);
        }

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            lock (info)
            {
                if (info.Context != null)
                {
                    (info.Context as FSNodeStream)?.Cleanup(info.DeleteOnClose);
                    info.Context = null;
                    if (info.DeleteOnClose)
                    {
                        try
                        {
                            var r = Provider.DeleteItem(fileName).Result;
                        }
                        catch (FileNotFoundException)
                        {
                            // This error can occur when creating a new file with the flag deleteOnclose set, because we won't loose time to upload the file
                        }

                    }
                }
            }
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            lock (info)
            {
                if (info.Context != null) {
                
                    (info.Context as FSNodeStream)?.Close();
                    info.Context = null;
                }
            }
        }

        public NtStatus CreateDirectory(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (mode == FileMode.CreateNew)
            {
                if (Provider.Exist(fileName))
                    return DokanResult.AlreadyExists;
                    
                FSNode n = Provider.CreateDir(fileName).Result;
                if(n==null) return DokanResult.PathNotFound;
                info.Context = new object();
                return DokanResult.Success;
            }

            FSNode node = Provider.FetchItem(fileName).Result;
            if (node == null) return DokanResult.PathNotFound;
            if (!node.IsDirectory) return DokanResult.NotADirectory;

            info.Context = new object();
            return DokanResult.Success;
        }

        public NtStatus CreateMainFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (info.IsDirectory)
                return CreateDirectory(fileName, access, share, mode, options, attributes, info);

            var node = Provider.FetchItem(fileName).Result;

            switch (mode)
            {
                case FileMode.Open:
                    if (node == null) return DokanResult.FileNotFound;
                    if (node.IsDirectory)
                    {
                        info.IsDirectory = node.IsDirectory;
                        info.Context = new object();
                        return DokanResult.Success;
                    }
                    break;
                case FileMode.CreateNew:
                    if (node != null) return DokanResult.FileExists;
                    break;
                case FileMode.Truncate:
                    if (node == null) return DokanResult.FileNotFound;
                    break;
            }

            // No access
            if((access & DataAccess) == 0) {
                info.Context = new object();
                return DokanResult.Success;
            }
            //Todo Check access
            var readAccess = (access & DataReadAccess) != 0;
            var writeAccess = (access & DataWriteAccess) != 0;


            var ioaccess = System.IO.FileAccess.Read;
            if (!readAccess && writeAccess)
            {
                ioaccess = System.IO.FileAccess.Write;
            }

            if (readAccess && writeAccess)
            {
                ioaccess = System.IO.FileAccess.ReadWrite;
            }

            var result = Provider.OpenFile(fileName, mode, ioaccess, share, options).Result;

            if (result == null)
            {
                return DokanResult.AccessDenied;
            }

            info.Context = result;
            return DokanResult.Success;
        }
        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            // Todo error handling
            return CreateMainFile(fileName, access, share, mode, options, attributes, info);
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            var items = Provider.GetDirItems(fileName).Result;
            files = items.Select(i => i.FileInfo).ToList();
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            if(Provider.Drive.Quota==null)
            {

                totalNumberOfBytes = 64L*1024*1024 * 1024 * 1024;
                freeBytesAvailable = totalNumberOfBytes;
                totalNumberOfFreeBytes = totalNumberOfBytes;
                return DokanResult.Success;
            }

            totalNumberOfBytes = (long)Provider.Drive.Quota;
            freeBytesAvailable = totalNumberOfBytes - long.Parse(Provider.Drive.Root.Size);
            totalNumberOfFreeBytes = freeBytesAvailable;
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            fileInfo = new FileInformation { FileName = fileName };
            var f = Provider.FetchItem(fileName).Result;
            if (f == null) return DokanResult.FileNotFound;
            fileInfo = f.FileInfo;
          
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, DokanFileInfo info)
        {
            volumeLabel = Provider.Drive.Name;
            features = FileSystemFeatures.None;
            fileSystemName = string.Empty;
            maximumComponentLength = 256;
            return DokanResult.Success;
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            return NtStatus.Success;
        }


        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            var node = Provider.MoveFile(oldName, newName, replace).Result;
            if (node == null) return DokanResult.Error;
            info.Context = node;
            return DokanResult.Success;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            bytesRead = 0;
            if ((info.Context) != null)
            {
                ((FSNodeStream)info.Context)?.Read(buffer,out bytesRead, offset, info);

                return NtStatus.Success;
            }

            bytesRead = 0;
            return DokanResult.Error;
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            var r = Provider.DeleteItem(fileName).Result;
            if (r)
            {
                info.Context = null;
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            var r = Provider.DeleteItem(fileName).Result;
            if (r)
            {
                info.Context = null;
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }


        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            bytesWritten = 0;
            if ((info.Context) != null)
            {
                if (info.WriteToEndOfFile)
                 ((FSNodeStream)info.Context)?.Append(buffer, out bytesWritten, info);
                else
                ((FSNodeStream)info.Context)?.Write(buffer, out bytesWritten, offset, info);

                return NtStatus.Success;
            }

            bytesWritten = 0;
            return DokanResult.Error;
        }


        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            files = new List<FileInformation>();
            return NtStatus.NotImplemented;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            ((FSNodeStream)info.Context)?.Flush();
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            security = null;
            return DokanResult.NotImplemented;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            (info.Context as FSNodeStream)?.SetLength(length);
            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }


        public void Mount()
        {
             _th = new Thread(new ThreadStart(() => {

#if LOG_ENABLED
                 this.Mount(this.DriveLetter + ":", DokanOptions.FixedDrive|DokanOptions.DebugMode, 10, Dokan.Version, new TimeSpan(0, 10, 0), new Logger(Provider.Drive.Name));
#else
                 this.Mount(this.DriveLetter + ":", DokanOptions.FixedDrive, 10, Dokan.Version, new TimeSpan(0, 10, 0), null);
#endif

             }));
            _th.Start();
        }


        public void Unmount()
        {
            if (_th != null)
            {
                _th.Abort();
            }

            Dokan.Unmount(this.DriveLetter);
            Dokan.RemoveMountPoint(this.DriveLetter + ":");
        }


    }
}
