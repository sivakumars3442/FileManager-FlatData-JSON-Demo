using System.IO.Compression;
using Syncfusion.Blazor.FileManager;
using static FileManager.Pages.Index;
using Microsoft.AspNetCore.Mvc;

namespace FileManager.Data
{
    public class PhysicalFileProvider 
    {
        public string contentRootPath;
        protected string[] allowedExtension = new string[] { "*" };
        AccessDetails AccessDetails = new AccessDetails();
        private string rootName = string.Empty;
        protected string hostPath;
        protected string hostName;
        private string accessMessage = string.Empty;
        internal HttpResponse Response;
        public string basePath;
        string root = "wwwroot\\Files";
        public List<CustomFileManagerDirectoryContent>? FileData;

        public PhysicalFileProvider(IWebHostEnvironment hostingEnvironment)
        {
            this.basePath = hostingEnvironment.ContentRootPath;
            RootFolder(this.basePath + "\\" + this.root);
        }

        public void RootFolder(string name)
        {
            this.contentRootPath = name;
            this.hostName = new Uri(contentRootPath).Host;
            if (!string.IsNullOrEmpty(this.hostName))
            {
                this.hostPath = Path.DirectorySeparatorChar + this.hostName + Path.DirectorySeparatorChar + contentRootPath.Substring((contentRootPath.ToLower().IndexOf(this.hostName) + this.hostName.Length + 1));
            }
        }

        public void SetRules(AccessDetails details)
        {
            this.AccessDetails = details;
            DirectoryInfo root = new DirectoryInfo(this.contentRootPath);
            this.rootName = root.Name;
        }

        public List<CustomFileManagerDirectoryContent> GetData()
        {
            List<CustomFileManagerDirectoryContent> Data = new List<CustomFileManagerDirectoryContent>();
            List<CustomFileManagerDirectoryContent> SubFolderList = new List<CustomFileManagerDirectoryContent>();
            FileManagerResponse<CustomFileManagerDirectoryContent> Response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
            Response = GetFiles("/", false, null);
            Data = new List<CustomFileManagerDirectoryContent>();
            Data.Add(Response.CWD);
            Data.AddRange(Response.Files);
            SubFolderList = new List<CustomFileManagerDirectoryContent>();
            SubFolderList = Response.Files.Where(file => !file.IsFile).ToList();
            for (int i = 0; i < SubFolderList.Count; i++)
            {
                string path = (SubFolderList[i].FilterPath + SubFolderList[i].Name).Replace(@"\", "/", StringComparison.Ordinal) + "/";
                FileManagerResponse<CustomFileManagerDirectoryContent> NestedData = new FileManagerResponse<CustomFileManagerDirectoryContent>();
                NestedData = GetFiles(path, false, SubFolderList[i]);
                Data.AddRange(NestedData.Files);
                List<CustomFileManagerDirectoryContent> NestedFiles = NestedData.Files
                                .Where(file => !file.IsFile)
                                .ToList();
                SubFolderList.AddRange(NestedFiles);
            }
            return Data;
        }
        public FileManagerResponse<CustomFileManagerDirectoryContent> GetFiles(string path, bool showHiddenItems, params CustomFileManagerDirectoryContent[] data)
        {
            FileManagerResponse<CustomFileManagerDirectoryContent> readResponse = new FileManagerResponse<CustomFileManagerDirectoryContent>();
            try
            {
                if (path == null)
                {
                    path = string.Empty;
                }
                String fullPath = (contentRootPath + path);
                DirectoryInfo directory = new DirectoryInfo(fullPath);
                string[] extensions = this.allowedExtension;
                CustomFileManagerDirectoryContent cwd = new CustomFileManagerDirectoryContent();
                string rootPath = string.IsNullOrEmpty(this.hostPath) ? this.contentRootPath : new DirectoryInfo(this.hostPath).FullName;
                string parentPath = string.IsNullOrEmpty(this.hostPath) ? directory.Parent.FullName : new DirectoryInfo(this.hostPath + (path != "/" ? path : "")).Parent.FullName;
                if(Path.GetFullPath(fullPath)!= GetFilePath(fullPath))
                {
                    throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                }
                cwd.Name = string.IsNullOrEmpty(this.hostPath) ? directory.Name : new DirectoryInfo(this.hostPath + path).Name;
                cwd.Size = 0;
                cwd.IsFile = false;
                cwd.DateModified = directory.LastWriteTime;
                cwd.DateCreated = directory.CreationTime;
                cwd.HasChild = CheckChild(directory.FullName);
                cwd.Type = directory.Extension;
                cwd.FilterPath = GetRelativePath(rootPath, parentPath + Path.DirectorySeparatorChar);
                cwd.Permission = GetPathPermission(path);
                if(path == "/")
                {
                    cwd.Id = 0.ToString();
                    cwd.ParentId = null;
                    cwd.FilterId = "";
                }
                readResponse.CWD = cwd;
                if (!hasAccess(directory.FullName) || (cwd.Permission != null && !cwd.Permission.Read))
                {
                    readResponse.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
                readResponse.Files = ReadDirectories(path, directory, extensions, showHiddenItems, data).Cast<CustomFileManagerDirectoryContent>().ToList();
                readResponse.Files = readResponse.Files.Concat(ReadFiles(path, directory, extensions, showHiddenItems, data)).Cast<CustomFileManagerDirectoryContent>().ToList();
                return readResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                readResponse.Error = er;
                return readResponse;
            }
        }

        protected virtual IEnumerable<CustomFileManagerDirectoryContent> ReadFiles(string path, DirectoryInfo directory, string[] extensions, bool showHiddenItems, params CustomFileManagerDirectoryContent[] data)
        {
            try
            {
                FileManagerResponse<CustomFileManagerDirectoryContent> readFiles = new FileManagerResponse<CustomFileManagerDirectoryContent>();
                if (!showHiddenItems)
                {
                    IEnumerable<CustomFileManagerDirectoryContent> files = extensions.SelectMany(directory.GetFiles).Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                            .Select(file => new CustomFileManagerDirectoryContent
                            {
                                Name = file.Name,
                                IsFile = true,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                DateCreated = file.CreationTime,
                                HasChild = false,
                                Type = file.Extension,
                                FilterPath = GetRelativePath(this.contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, file.Name, true),
                                Id = Math.Abs(Guid.NewGuid().GetHashCode()).ToString(),
                                ParentId = data == null ? 0.ToString() : data[0].Id,
                                FilterId = data == null ? 0.ToString() + "/" : data[0].FilterId + data[0].Id + "/"
                            });
                    readFiles.Files = files.Cast<CustomFileManagerDirectoryContent>().ToList();
                }
                else
                {
                    IEnumerable<CustomFileManagerDirectoryContent> files = extensions.SelectMany(directory.GetFiles)
                            .Select(file => new CustomFileManagerDirectoryContent
                            {
                                Name = file.Name,
                                IsFile = true,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                DateCreated = file.CreationTime,
                                HasChild = false,
                                Type = file.Extension,
                                FilterPath = GetRelativePath(this.contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, file.Name, true)
                            });
                    readFiles.Files = files.Cast<CustomFileManagerDirectoryContent>().ToList();
                }
                FileStreamResult fileStream;
                for (int i = 0; i < readFiles.Files.Count; i++)
                {
                    fileStream = Download(path, new string[] { readFiles.Files[i].Name }, readFiles.Files[i]);
                    readFiles.Files[i].StreamData = fileStream.FileStream;
                }
                return readFiles.Files;
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected string GetRelativePath(string rootPath, string fullPath)
        {
            if (!String.IsNullOrEmpty(rootPath) && !String.IsNullOrEmpty(fullPath))
            {
                DirectoryInfo rootDirectory;
                if (!string.IsNullOrEmpty(this.hostName))
                {
                    if (rootPath.Contains(this.hostName) || rootPath.ToLower().Contains(this.hostName) || rootPath.ToUpper().Contains(this.hostName))
                    {
                        rootPath = rootPath.Substring(rootPath.IndexOf(this.hostName, StringComparison.CurrentCultureIgnoreCase) + this.hostName.Length);
                    }
                    if (fullPath.Contains(this.hostName) || fullPath.ToLower().Contains(this.hostName) || fullPath.ToUpper().Contains(this.hostName))
                    {
                        fullPath = fullPath.Substring(fullPath.IndexOf(this.hostName, StringComparison.CurrentCultureIgnoreCase) + this.hostName.Length);
                    }
                    rootDirectory = new DirectoryInfo(rootPath);
                    fullPath = new DirectoryInfo(fullPath).FullName;
                    rootPath = new DirectoryInfo(rootPath).FullName;
                }
                else
                {
                    rootDirectory = new DirectoryInfo(rootPath);
                }
                if (rootDirectory.FullName.Substring(rootDirectory.FullName.Length - 1) == Path.DirectorySeparatorChar.ToString())
                {
                    if (fullPath.Contains(rootDirectory.FullName))
                    {
                        return fullPath.Substring(rootPath.Length - 1);
                    }
                }
                else if (fullPath.Contains(rootDirectory.FullName + Path.DirectorySeparatorChar))
                {
                    return Path.DirectorySeparatorChar + fullPath.Substring(rootPath.Length + 1);
                }
            }
            return String.Empty;
        }


        protected virtual IEnumerable<CustomFileManagerDirectoryContent> ReadDirectories(string path, DirectoryInfo directory, string[] extensions, bool showHiddenItems, params CustomFileManagerDirectoryContent[] data)
        {
            FileManagerResponse<CustomFileManagerDirectoryContent> readDirectory = new FileManagerResponse<CustomFileManagerDirectoryContent>();
            try
            {
                if (!showHiddenItems)
                {
                    //var directories = directory.GetDirectories().Where(f => (f.Attributes & FileAttributes.Hidden) == 0);

                    //List<CustomFileManagerDirectoryContent> fileList = new List<CustomFileManagerDirectoryContent>();

                    //foreach (var subDirectory in directories)
                    //{
                    //    CustomFileManagerDirectoryContent content = new CustomFileManagerDirectoryContent
                    //    {
                    //        Name = subDirectory.Name,
                    //        Size = 0,
                    //        IsFile = false,
                    //        DateModified = subDirectory.LastWriteTime,
                    //        DateCreated = subDirectory.CreationTime,
                    //        HasChild = CheckChild(subDirectory.FullName),
                    //        Type = subDirectory.Extension,
                    //        FilterPath = GetRelativePath(this.contentRootPath, directory.FullName),
                    //        Permission = GetPermission(directory.FullName, subDirectory.Name, false),
                    //        Id = Math.Abs(Guid.NewGuid().GetHashCode()).ToString(),
                    //        ParentId = data?.FirstOrDefault()?.Id ?? "0",
                    //        FilterId = data?.FirstOrDefault()?.FilterId + data?.FirstOrDefault()?.Id + "/" ?? "0/"
                    //    };

                    //    fileList.Add(content);
                    //}

                    //readDirectory.Files = fileList;

                    IEnumerable<CustomFileManagerDirectoryContent> directories = directory.GetDirectories().Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                            .Select(subDirectory => new CustomFileManagerDirectoryContent
                            {
                                Name = subDirectory.Name,
                                Size = 0,
                                IsFile = false,
                                DateModified = subDirectory.LastWriteTime,
                                DateCreated = subDirectory.CreationTime,
                                HasChild = CheckChild(subDirectory.FullName),
                                Type = subDirectory.Extension,
                                FilterPath = GetRelativePath(this.contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, subDirectory.Name, false),
                                Id = Math.Abs(Guid.NewGuid().GetHashCode()).ToString(),
                                ParentId = data == null ? 0.ToString() : data[0].Id,
                                FilterId = data == null ? 0.ToString() + "/" : data[0].FilterId + data[0].Id + "/"

                            });
                    readDirectory.Files = directories.Cast<CustomFileManagerDirectoryContent>().ToList();
                }
                else
                {
                    IEnumerable<CustomFileManagerDirectoryContent> directories = directory.GetDirectories().Select(subDirectory => new CustomFileManagerDirectoryContent
                    {
                        Name = subDirectory.Name,
                        Size = 0,
                        IsFile = false,
                        DateModified = subDirectory.LastWriteTime,
                        DateCreated = subDirectory.CreationTime,
                        HasChild = CheckChild(subDirectory.FullName),
                        Type = subDirectory.Extension,
                        FilterPath = GetRelativePath(this.contentRootPath, directory.FullName),
                        Permission = GetPermission(directory.FullName, subDirectory.Name, false)
                    });
                    readDirectory.Files = directories.Cast<CustomFileManagerDirectoryContent>().ToList();
                }
                FileStreamResult fileStream;
                for(int i =0; i< readDirectory.Files.Count; i++)
                {
                    fileStream = Download(path, new string[] { readDirectory.Files[i].Name }, readDirectory.Files[i]);
                    readDirectory.Files[i].StreamData = fileStream.FileStream;
                }
                return readDirectory.Files;
            }
            catch (Exception)
            {
                throw;
            }
        }
               
        public virtual FileStreamResult Download(string path, string[] names, params CustomFileManagerDirectoryContent[] data)
        {
            try
            {
                string validatePath;
                string physicalPath = GetPath(path);
                String fullPath;
                int count = 0;
                validatePath = Path.Combine(contentRootPath + path);
                if (Path.GetFullPath(validatePath) != GetFilePath(validatePath))
                {
                    throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                }
                for (int i = 0; i < names.Length; i++)
                {
                    bool IsFile = !IsDirectory(physicalPath, names[i]);
                    AccessPermission FilePermission = GetPermission(physicalPath, names[i], IsFile);
                    if (FilePermission != null && (!FilePermission.Read || !FilePermission.Download))
                    {
                        throw new UnauthorizedAccessException("'" + this.rootName + path + names[i] + "' is not accessible. Access is denied.");
                    }
                    fullPath = Path.Combine(contentRootPath + path, names[i]);
                    if (Path.GetFullPath(fullPath) != GetFilePath(fullPath) + names[i])
                    {
                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                    }
                    if ((File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory)
                    {
                        count++;
                    }
                }
                if (count == names.Length)
                {
                    return DownloadFile(path, names);
                }
                else
                {
                    return DownloadFolder(path, names, count);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private FileStreamResult fileStreamResult;
        protected virtual FileStreamResult DownloadFile(string path, string[] names = null)
        {
            try
            {
                path = Path.GetDirectoryName(path);
                string tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                String fullPath;
                if (names == null || names.Length == 0)
                {
                    fullPath = (contentRootPath + path);
                    if (Path.GetFullPath(fullPath) != GetFilePath(fullPath) + names[0])
                    {
                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                    }
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    FileStream fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                }
                else if (names.Length == 1)
                {
                    fullPath = Path.Combine(contentRootPath + path, names[0]);
                    if (Path.GetFullPath(fullPath) != GetFilePath(fullPath) + names[0])
                    {
                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                    }
                    FileStream fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = names[0];
                }
                else if (names.Length > 1)
                {
                    string fileName = Guid.NewGuid().ToString() + "temp.zip";
                    string newFileName = fileName.Substring(36);
                    tempPath = Path.Combine(Path.GetTempPath(), newFileName);
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                    string currentDirectory;
                    ZipArchiveEntry zipEntry;
                    ZipArchive archive;
                    for (int i = 0; i < names.Count(); i++)
                    {
                        fullPath = Path.Combine((contentRootPath + path), names[i]);
                        if (Path.GetFullPath(fullPath) != GetFilePath(fullPath) + names[i])
                        {
                            throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                        }
                        if (!string.IsNullOrEmpty(fullPath))
                        {
                            try
                            {
                                using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                                {
                                    currentDirectory = Path.Combine((contentRootPath + path), names[i]);
                                    if (Path.GetFullPath(currentDirectory) != GetFilePath(currentDirectory) + names[i])
                                    {
                                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                                    }
                                    zipEntry = archive.CreateEntryFromFile(Path.Combine(this.contentRootPath, currentDirectory), names[i], CompressionLevel.Fastest);
                                }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                        }
                        else
                        {
                            throw new ArgumentNullException("name should not be null");
                        }
                    }
                    try
                    {
                        FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                        fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                        fileStreamResult.FileDownloadName = "files.zip";
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return fileStreamResult;
            }
            catch (Exception)
            {
                throw;
            }
        }
        protected FileStreamResult DownloadFolder(string path, string[] names, int count)
        {
            try
            {
                if (!String.IsNullOrEmpty(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                FileStreamResult fileStreamResult;
                // create a temp.Zip file intially 
                string tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                String fullPath;
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                if (names.Length == 1)
                {
                    fullPath = Path.Combine(contentRootPath + path, names[0]);
                    if (Path.GetFullPath(fullPath) != GetFilePath(fullPath) + names[0])
                    {
                        throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                    }
                    DirectoryInfo directoryName = new DirectoryInfo(fullPath);
                    ZipFile.CreateFromDirectory(fullPath, tempPath, CompressionLevel.Fastest, true);
                    FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = directoryName.Name + ".zip";
                }
                else
                {
                    string currentDirectory;
                    ZipArchiveEntry zipEntry;
                    ZipArchive archive;
                    using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            currentDirectory = Path.Combine((contentRootPath + path), names[i]);
                            if (Path.GetFullPath(currentDirectory) != GetFilePath(currentDirectory) + names[i])
                            {
                                throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                            }
                            if ((File.GetAttributes(currentDirectory) & FileAttributes.Directory) == FileAttributes.Directory)
                            {
                                string[] files = Directory.GetFiles(currentDirectory, "*.*", SearchOption.AllDirectories);
                                if (files.Length == 0)
                                {
                                    zipEntry = archive.CreateEntry(names[i] + "/");
                                }
                                else
                                {
                                    foreach (string filePath in files)
                                    {
                                        zipEntry = archive.CreateEntryFromFile(filePath, names[i] + filePath.Substring(currentDirectory.Length), CompressionLevel.Fastest);
                                    }
                                }
                                foreach (string filePath in Directory.GetDirectories(currentDirectory, "*", SearchOption.AllDirectories))
                                {
                                    if (Directory.GetFiles(filePath).Length == 0)
                                    {
                                        zipEntry = archive.CreateEntry(names[i] + filePath.Substring(currentDirectory.Length) + "/");
                                    }
                                }
                            }
                            else
                            {
                                zipEntry = archive.CreateEntryFromFile(Path.Combine(this.contentRootPath, currentDirectory), names[i], CompressionLevel.Fastest);
                            }
                        }
                    }
                    FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "application/force-download");
                    fileStreamResult.FileDownloadName = "folders.zip";
                }
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return fileStreamResult;
            }
            catch (Exception)
            {
                throw;
            }
        }
        protected virtual AccessPermission GetPermission(string location, string name, bool isFile)
        {
            AccessPermission FilePermission = new AccessPermission();
            if (isFile)
            {
                if (this.AccessDetails.AccessRules == null) return null;
                string nameExtension = Path.GetExtension(name).ToLower();
                string fileName = Path.GetFileNameWithoutExtension(name);
                string currentPath = GetFilePath(location + name);
                foreach (AccessRule fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Path) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (fileRule.Path.IndexOf("*.*") > -1)
                        {
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*.*"));
                            if (currentPath.IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf("*.") > -1)
                        {
                            string pathExtension = Path.GetExtension(fileRule.Path).ToLower();
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*."));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && nameExtension == pathExtension)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf(".*") > -1)
                        {
                            string pathName = Path.GetFileNameWithoutExtension(fileRule.Path);
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf(pathName + ".*"));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && fileName == pathName)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (GetPath(fileRule.Path) == GetValidPath(location + name))
                        {
                            FilePermission = UpdateFileRules(FilePermission, fileRule);
                        }
                    }
                }
                return FilePermission;
            }
            else
            {
                if (this.AccessDetails.AccessRules == null) { return null; }
                foreach (AccessRule folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Path != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (folderRule.Path.IndexOf("*") > -1)
                        {
                            string parentPath = folderRule.Path.Substring(0, folderRule.Path.IndexOf("*"));
                            if (GetValidPath(location + name).IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFolderRules(FilePermission, folderRule);
                            }
                        }
                        else if (GetPath(folderRule.Path) == GetValidPath(location + name) || GetPath(folderRule.Path) == GetValidPath(location + name + Path.DirectorySeparatorChar))
                        {
                            FilePermission = UpdateFolderRules(FilePermission, folderRule);
                        }
                        else if (GetValidPath(location + name).IndexOf(GetPath(folderRule.Path)) == 0)
                        {
                            FilePermission.Write = HasPermission(folderRule.WriteContents);
                            FilePermission.WriteContents = HasPermission(folderRule.WriteContents);
                        }
                    }
                }
                return FilePermission;
            }
        }
        protected virtual string GetPath(string path)
        {
            String fullPath = (this.contentRootPath + path);
            DirectoryInfo directory = new DirectoryInfo(fullPath);
            return directory.FullName;
        }
        protected virtual string GetValidPath(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            return directory.FullName;
        }
        protected virtual string GetFilePath(string path)
        {
            return Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
        }
        protected virtual string[] GetFolderDetails(string path)
        {
            string[] str_array = path.Split('/'), fileDetails = new string[2];
            string parentPath = "";
            for (int i = 0; i < str_array.Length - 2; i++)
            {
                parentPath += str_array[i] + "/";
            }
            fileDetails[0] = parentPath;
            fileDetails[1] = str_array[str_array.Length - 2];
            return fileDetails;
        }
        protected virtual AccessPermission GetPathPermission(string path)
        {
            string[] fileDetails = GetFolderDetails(path);
            return GetPermission(GetPath(fileDetails[0]), fileDetails[1], false);
        }
        protected virtual AccessPermission GetFilePermission(string path)
        {
            string parentPath = path.Substring(0, path.LastIndexOf("/") + 1);
            string fileName = Path.GetFileName(path);
            return GetPermission(GetPath(parentPath), fileName, true);
        }
        protected virtual bool IsDirectory(string path, string fileName)
        {
            try
            {
                string fullPath = Path.Combine(path, fileName);
                FileAttributes attributes = File.GetAttributes(fullPath);

                return ((attributes & FileAttributes.Directory) != FileAttributes.Directory) ? false : true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        protected virtual bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }
        protected virtual AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? string.Empty : fileRule.Message;
            return filePermission;
        }
        protected virtual AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? string.Empty : folderRule.Message;
            return folderPermission;
        }
        protected virtual bool parentsHavePermission(FileManagerDirectoryContent fileDetails)
        {
            String parentPath = fileDetails.FilterPath.Replace(Path.DirectorySeparatorChar, '/');
            String[] parents = parentPath.Split('/');
            String currPath = "/";
            bool hasPermission = true;
            for (int i = 0; i <= parents.Length - 2; i++)
            {
                currPath = (parents[i] == "") ? currPath : (currPath + parents[i] + "/");
                AccessPermission PathPermission = GetPathPermission(currPath);
                if (PathPermission == null)
                {
                    break;
                }
                else if (PathPermission != null && !PathPermission.Read)
                {
                    hasPermission = false;
                    break;
                }
            }
            return hasPermission;
        }

        private bool CheckChild(string path)
        {
            bool hasChild;
            try
            {
                DirectoryInfo directory = new DirectoryInfo(path);
                DirectoryInfo[] dir = directory.GetDirectories();
                hasChild = dir.Length != 0;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    hasChild = false;
                }
                else
                {
                    throw;
                }
            }
            return hasChild;
        }
        private bool hasAccess(string path)
        {
            bool hasAcceess;
            try
            {
                DirectoryInfo directory = new DirectoryInfo(path);
                DirectoryInfo[] dir = directory.GetDirectories();
                hasAcceess = dir != null;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    hasAcceess = false;
                }
                else
                {
                    throw;
                }
            }
            return hasAcceess;
        }
        private long GetDirectorySize(DirectoryInfo dir, long size)
        {
            try
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    size = GetDirectorySize(subdir, size);
                }
                foreach (FileInfo file in dir.GetFiles())
                {
                    size += file.Length;
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw;
                }
            }
            return size;
        }
        private List<FileInfo> GetDirectoryFiles(DirectoryInfo dir, List<FileInfo> files)
        {
            try
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    files = GetDirectoryFiles(subdir, files);
                }
                foreach (FileInfo file in dir.GetFiles())
                {
                    files.Add(file);
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw;
                }
            }
            return files;
        }
        private List<DirectoryInfo> GetDirectoryFolders(DirectoryInfo dir, List<DirectoryInfo> files)
        {
            try
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    files = GetDirectoryFolders(subdir, files);
                }
                foreach (DirectoryInfo file in dir.GetDirectories())
                {
                    files.Add(file);
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw;
                }
            }
            return files;
        }

    }
}
