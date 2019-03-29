using DokanNet;
using Drive.Atonline.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drive.Atonline
{
    public class DriveProvider
    {
        public Drive.Atonline.Rest.Drive Drive { get; }
        private readonly FileTree _fileTree = new FileTree();
        private FSNode _root;

        private readonly HashSet<string> excludedFiles = new HashSet<string>
        {
            "desktop.ini",
        };

        public DriveProvider(Rest.Drive drive)
        {
            Drive = drive;
            _root = new FSNode(Drive.Root);
        }


        public async Task<DriveItem> Browse(FSNode parent, string path)
        {
            var p = path.Replace('\\', '/');
            var d = (await RestClient.Api<RestResponse<DriveItem>>($"Drive/Item/{parent.Item.Drive_Item__}:browse", "POST", new Dictionary<string, object>() { { "path", p } })).data;
            if (d==null || d.Drive_Item__ == null)
            {
                return null;
            }
            return d;
        }

        public async Task<DriveItem> Browse(string path)
        {
            return await Browse(_root, path);
        }

        public async Task<List<DriveItem>> BrowseChildren(FSNode node)
        {
            return await RestClient.retrieveAll<DriveItem>("Drive/" + Drive.Drive__ + "/Item", "GET", new Dictionary<string, object>() { { "Parent_Drive_Item__", node.Item.Drive_Item__} });
        }

        public bool Exist(string fileName)
        {
            return FetchItem(fileName).Result != null;
        }

        public async Task<FSNode> FetchItem(string path)
        {
            if (path == "\\" || path == string.Empty) return _root;

            if (!path.StartsWith("\\")) path = "\\" + path;

            if (excludedFiles.Contains(System.IO.Path.GetFileName(path)))
            {
                return null;
            }

            FSNode item = _fileTree.GetItem(path); ;
           
            if (item != null) return item;
    
            var di = await Browse(path);
            if (di == null) return null;
            // item = new FSNode(di, System.IO.Path.GetDirectoryName(path));
            // _fileTree.Add(item);


            var folders = new LinkedList<string>();
            var curpath = path;
            item = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath))
                {
                    break;
                }

                item = _fileTree.GetItem(curpath);
            }
            while (item == null);

            if (item == null)
            {
                item = _root;
            }

            if (curpath == "\\")
            {
                curpath = string.Empty;
            }

            foreach (var name in folders)
            {
                var newpath = curpath + "\\" + name;

                var newnode = await Browse(item, name);
                if (newnode == null)
                {
                    // Log.Error("NonExisting path from server: " + itemPath);
                    return null;
                }

                item = new FSNode(newnode, item.Path);
                _fileTree.Add(item);
                curpath = newpath;
            }

            return item;
        }

        public async Task<FSNode> CreateDir(string path)
        {
            var parent = await FetchItem(Path.GetDirectoryName(path));
            if (parent == null) return null;
            var node = await CreateDir(parent, Path.GetFileName(path));
            if (node == null) return null;
            _fileTree.Add(node);

            return node;
        }

        public async Task<FSNode> CreateDir(FSNode parent, string directoryName)
        {
            var r = (await RestClient.Api<RestResponse<DriveItem>>($"Drive/{Drive.Drive__}/Item", "POST", new Dictionary<string, object>() { { "Name", directoryName }, { "Parent_Drive_Item__", parent.Item.Drive_Item__ } })).data;
            if (r == null || r.Drive_Item__ == null)
                return null;
            return new FSNode(r, parent.Path);
        }

        public async Task<List<FSNode>> GetDirItems(string path)
        {
            var cached = _fileTree.GetDir(path);

            if (cached != null)
            {
                return (await Task.WhenAll(cached.Select(FetchItem))).Where(i => i != null).ToList();
            }


            var item = await FetchItem(path);
            var childrens = await BrowseChildren(item);
            List<FSNode> nodes = new List<FSNode>(childrens.Count);

            foreach (var c in childrens)
            {
                nodes.Add(new FSNode(c, path));
            }

            _fileTree.AddDirItems(item, nodes);

            return nodes;
        }

        public async Task<FSNodeStream> OpenFile(string fileName, FileMode mode, System.IO.FileAccess ioaccess, FileShare share, FileOptions options)
        {
            var item = await FetchItem(fileName);
            if (ioaccess == System.IO.FileAccess.Read)
            {
                   return item?.getStream();
            }
             

            if(item == null)
            {
                var parentPath = System.IO.Path.GetDirectoryName(fileName);
                var parent = await FetchItem(parentPath);
                if (parent == null) throw new DirectoryNotFoundException();

                var newItem = new FSNode(new DriveItem() { Name = System.IO.Path.GetFileName(fileName), Parent_Drive_Item__ = parent.Item.Drive_Item__ }, parentPath);
                _fileTree.Add(newItem);
                return newItem.getStream() ;
            }


            return item.getStream();
            /*if (mode == FileMode.Create || mode == FileMode.Truncate)
            {
                return item.getStream();
                //throw new NotImplementedException("Open File in truncate mode");
            }

            if (mode == FileMode.Open || mode == FileMode.Append || mode == FileMode.OpenOrCreate)
            {
                return item.getStream();
                throw new NotImplementedException("Open File in append mode");
            }*/
        }

        public async Task<FSNode> MoveFile(string oldFilePath, string newFilePath, bool replace)
        {
          
            var item = await FetchItem(oldFilePath);
            if (item == null) throw new InvalidOperationException("Path Not Found");

            if (oldFilePath == newFilePath) return item;

            var newName = Path.GetFileName(newFilePath);
            var oldName = Path.GetFileName(oldFilePath);
            var newParentItem = await FetchItem(Path.GetDirectoryName(newFilePath));
            if(newParentItem == null) throw new InvalidOperationException("Path Not Found");
            if (!newParentItem.IsDirectory)  throw new InvalidOperationException("Not a directory");

            var node = await MoveFile(item, newParentItem, newName == oldName ? null : newName, replace);
            if(node == null)
            {
                throw new InvalidOperationException("Cannot move file");
            }

            try
            {
                _fileTree.Move(oldFilePath, node);
            }catch(Exception e)
            {
                //Todo rollback Move File ?? or don't care ?
                throw e;
            }

            return node;
        }

        public async Task<bool> DeleteItem(string fileName)
        {
            var item = await FetchItem(fileName);
            if (item == null) throw new FileNotFoundException($"Failed to delete {fileName}", fileName);

            if(!await DeleteItem(item))
            {
                throw new Exception("Unable to delete the file");
            }

            if(item.IsDirectory)
                _fileTree.DeleteDir(fileName);
            else
                _fileTree.DeleteFile(fileName);

            return true;
        }

        public async Task<bool> DeleteItem(FSNode node)
        {
            return (await RestClient.Api<RestResponse<object>>($"Drive/Item/{node.Item.Drive_Item__}", "DELETE")) != null;
        }

        public async Task<FSNode> MoveFile(FSNode file, FSNode target, string rename = null, bool replace = false)
        {
            Dictionary<string, object> p = new Dictionary<string, object>();
            p.Add("target", target.Item.Drive_Item__);
            if(rename!=null)
            p.Add("rename", rename);
            p.Add("overwrite", replace);

            var r = (await RestClient.Api<RestResponse<DriveItem>>($"Drive/Item/{file.Item.Drive_Item__}:moveTo", "POST", p)).data;
            if (r == null || r.Drive_Item__ == null)
                return null;

            return new FSNode(r, target.Path);
        }
    }
}
