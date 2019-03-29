using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Drive.Atonline
{
    public class FileTree : IDisposable
    {
        private class DirItem
        {
            public DirItem(IEnumerable<string> items, FSNode Item, int expiresIn)
            {
                Items = new HashSet<string>(items);
                this.Item = Item;
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            }

            public HashSet<string> Items { get; }
            public FSNode Item { get; }
            public DateTime ExpiresAt { get; private set; }


            public bool IsExpired  => DateTime.UtcNow > ExpiresAt;
        }

        public FileTree()
        {
            Task.Factory.StartNew(async () => await Cleaner(), TaskCreationOptions.LongRunning);
        }

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ReaderWriterLockSlim lok = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, DirItem> pathToDirItem = new Dictionary<string, DirItem>();
        private readonly Dictionary<string, FSNode> pathToNode = new Dictionary<string, FSNode>();
        private bool disposedValue;

        public int DirItemsExpirationSeconds { get; set; } = 60;
        public int FSItemsExpirationSeconds { get; set; } =  5*60;

        public void Clear()
        {
            lok.EnterWriteLock();
            try
            {
                pathToDirItem.Clear();
                pathToNode.Clear();
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }

            if (disposing)
            {
                lok.Dispose();
                cancellation.Dispose();
            }

            disposedValue = true;
        }

        public FSNode GetItem(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToNode.TryGetValue(filePath, out FSNode item))
                {
                    return null;
                }

                return item;
            }
            finally
            {
                lok.ExitUpgradeableReadLock();
            }
        }

        public IEnumerable<string> GetDir(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToDirItem.TryGetValue(filePath, out DirItem item))
                {
                    return null;
                }

                if (!item.IsExpired)
                {
                    return item.Items;
                }

                lok.EnterWriteLock();
                try
                {
                    pathToDirItem.Remove(filePath);
                    return null;
                }
                finally
                {
                    lok.ExitWriteLock();
                }

            }
            finally
            {
                lok.ExitUpgradeableReadLock();
            }
        }

        public void Add(FSNode item)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[item.Path] = item;
                if (pathToDirItem.TryGetValue(item.Dir, out DirItem dirItem))
                {
                    dirItem.Items.Add(item.Path);
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void AddDirItems(FSNode parent, List<FSNode> items)
        {
            lok.EnterWriteLock();
            try
            {
                pathToDirItem[parent.Path] = new DirItem(items.Select(i => i.Path).ToList(), parent, DirItemsExpirationSeconds);
                foreach (var item in items)
                {
                    pathToNode[item.Path] = item;
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        private async Task Cleaner()
        {
            var token = cancellation.Token;
            while (!token.IsCancellationRequested && !disposedValue)
            {
                try
                {
                    lok.EnterWriteLock();
                    try
                    {
                        foreach (var key  in pathToNode.Where(p => p.Value.IsExpired(FSItemsExpirationSeconds)).ToList())
                        {
                            pathToNode.Remove(key.Key);
                        }
                    }
                    finally
                    {
                        lok.ExitWriteLock();
                    }

                    lok.EnterWriteLock();
                    try
                    {
                        foreach (var key in pathToDirItem.Where(p => p.Value.IsExpired).ToList())
                        {
                            pathToNode.Remove(key.Key);
                        }
                    }
                    finally
                    {
                        lok.ExitWriteLock();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("WARNING ------" + ex.Message);
                }

                await Task.Delay(FSItemsExpirationSeconds /** 6*/, token);
            }
        }

        public void DeleteDir(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                if (dirPath == null)
                {
                    throw new InvalidOperationException($"dirPath is null for '{filePath}'");
                }

                if (pathToDirItem.TryGetValue(dirPath, out DirItem dirItem))
                {
                    dirItem.Items.RemoveWhere(i => i == filePath);
                }

                foreach (var kv in pathToNode.Where(kv => kv.Key.StartsWith(filePath, StringComparison.InvariantCulture)).ToList())
                {
                    pathToNode.Remove(kv.Key);
                }

                foreach (var kv in pathToDirItem.Where(kv => kv.Key.StartsWith(filePath, StringComparison.InvariantCulture)).ToList())
                {
                    pathToDirItem.Remove(kv.Key);
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void MoveDir(string oldPath, FSNode newNode)
        {
            if (!newNode.IsDirectory) return;
            lok.EnterWriteLock();
            try
            {
                DeleteDir(oldPath);
                Add(newNode);
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void MoveFile(string oldPath, FSNode newNode)
        {
            if (newNode.IsDirectory) return;
            lok.EnterWriteLock();
            try
            {
                DeleteFile(oldPath);
                Add(newNode);
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void DeleteFile(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                if (dirPath == null)
                {
                    throw new InvalidOperationException($"dirPath is null for '{filePath}'");
                }

                if (pathToDirItem.TryGetValue(dirPath, out DirItem dirItem))
                {
                    dirItem.Items.Remove(filePath);
                }

                pathToNode.Remove(filePath);
            }
            finally
            {
                // Log.Warn("File deleted: " + filePath);
                lok.ExitWriteLock();
            }
        }

        public void Move(string oldPath, FSNode newNode)
        {
            if (newNode.IsDirectory) MoveDir(oldPath, newNode);
            else MoveFile(oldPath, newNode);
        }
    }
}
