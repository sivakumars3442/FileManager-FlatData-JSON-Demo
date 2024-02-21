using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Syncfusion.Blazor.FileManager;
using System.IO;
using System.Xml.Linq;
using static FileManager.Pages.Index;

namespace FileManager.Data
{
	public class FileService
	{
		public string basePath { get; set; }
		public FileService(IWebHostEnvironment webHostEnvironment)
		{
			basePath = webHostEnvironment.ContentRootPath + "/wwwroot/FileManager/Syncfusion.png";
		}
        List<FileManagerDirectoryContent> copyFiles = new List<FileManagerDirectoryContent>();

        public FileManagerResponse<CustomFileManagerDirectoryContent> Read(string path, List<CustomFileManagerDirectoryContent> dataSource, CustomFileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			if (path == "/")
			{
				string ParentId = dataSource
					.Where(x => x.FilterPath == string.Empty)
					.Select(x => x.Id).First();
				response.CWD = dataSource
					.Where(x => x.FilterPath == string.Empty)
					.Select(x => new CustomFileManagerDirectoryContent()
					{
						Id = x.Id,
						Name = x.Name,
						Size = x.Size,
						DateCreated = x.DateCreated,
						DateModified = x.DateModified,
						Type = Path.GetExtension(x.Name),
						HasChild = x.HasChild,
						ParentId = "",
						FilterPath = x.FilterPath,
						FilterId = x.FilterId,
						IsFile = x.IsFile,
					}).First();
				response.Files = dataSource
					.Where(x => x.ParentId == ParentId)
					.Select(x => new CustomFileManagerDirectoryContent()
					{
						Id = x.Id,
						Name = x.Name,
						Size = x.Size,
						DateCreated = x.DateCreated,
						DateModified = x.DateModified,
						Type = Path.GetExtension(x.Name),
						ParentId = "0",
						HasChild = x.HasChild,
						FilterPath = x.FilterPath,
						FilterId = x.FilterId,
						IsFile = x.IsFile,
					}).ToList();
			}
			else
			{
				var id = fileDetails.Length > 0 && fileDetails[0] != null ? fileDetails[0].Id : dataSource
					.Where(x => x.FilterPath == path)
					.Select(x => x.ParentId).First();
				response.CWD = dataSource
					.Where(x => x.Id == (fileDetails.Length > 0 && fileDetails[0] != null ? fileDetails[0].Id : id))
					.Select(x => new CustomFileManagerDirectoryContent()
					{
						Id = x.Id,
						Name = x.Name,
						Size = x.Size,
						DateCreated = x.DateCreated,
						DateModified = x.DateModified,
						Type = Path.GetExtension(x.Name),
						HasChild = x.HasChild,
						ParentId = x.ParentId,
						FilterPath = x.FilterPath,
						FilterId = x.FilterId,
						IsFile = x.IsFile,
					}).First();
				response.Files = dataSource
					.Where(x => x.ParentId == (fileDetails.Length > 0 && fileDetails[0] != null ? fileDetails[0].Id : id))
					.Select(x => new CustomFileManagerDirectoryContent()
					{
						Id = x.Id,
						Name = x.Name,
						Size = x.Size,
						DateCreated = x.DateCreated,
						DateModified = x.DateModified,
						Type = x.Type,
						ParentId = (fileDetails.Length > 0 && fileDetails[0] != null ? fileDetails[0].Id : id),
						HasChild = x.HasChild,
						FilterPath = x.FilterPath,
						FilterId = x.FilterId,
						IsFile = x.IsFile,
					}).ToList();
			}
			return response;
		}
		public FileManagerResponse<FileManagerDirectoryContent> Delete(string path, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
			var idsToDelete = fileDetails.Cast<FileManagerDirectoryContent>().Select(x => x.Id).ToList();
			idsToDelete.AddRange(dataSource.Where(file => idsToDelete.Contains((file as FileManagerDirectoryContent).ParentId)).Select(file => (file as FileManagerDirectoryContent).Id));
			dataSource.RemoveAll(file => idsToDelete.Contains((file as FileManagerDirectoryContent).Id));
			response.Files = fileDetails.ToList();
			return response;
		}

		public FileManagerResponse<FileManagerDirectoryContent> Details(string path, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
			string RootDirectoryName = dataSource
				.Where(x => x.FilterPath == string.Empty)
				.Select(x => x.Name).First();
			FileDetails Details = new FileDetails();
			if (fileDetails.Length == 0 || fileDetails.Length == 1)
			{
				Details.Created = (fileDetails[0] as FileManagerDirectoryContent).DateCreated.ToString();
				Details.IsFile = (fileDetails[0] as FileManagerDirectoryContent).IsFile;
				Details.Location = RootDirectoryName == (fileDetails[0] as FileManagerDirectoryContent).Name ? RootDirectoryName : RootDirectoryName + (fileDetails[0] as FileManagerDirectoryContent).FilterPath + (fileDetails[0] as FileManagerDirectoryContent).Name;
				Details.Modified = (fileDetails[0] as FileManagerDirectoryContent).DateModified.ToString();
				Details.Name = (fileDetails[0] as FileManagerDirectoryContent).Name;
				Details.Permission = (fileDetails[0] as FileManagerDirectoryContent).Permission;
				Details.Size = byteConversion((fileDetails[0] as FileManagerDirectoryContent).Size);

			}
			else
			{
				string previousName = string.Empty;
				Details.Size = "0";
				for (int i = 0; i < fileDetails.Length; i++)
				{
					Details.Name = string.IsNullOrEmpty(previousName) ? previousName = (fileDetails[i] as FileManagerDirectoryContent).Name : previousName = previousName + ", " + (fileDetails[i] as FileManagerDirectoryContent).Name; ;
					Details.Size = long.Parse(Details.Size) + (fileDetails[i] as FileManagerDirectoryContent).Size + "";
					Details.MultipleFiles = true;
				}
				Details.Size = byteConversion(long.Parse(Details.Size));
			}
			response.Details = Details;
			return response;
		}

		protected String byteConversion(long fileSize)
		{
			try
			{
				string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
				if (fileSize == 0)
				{
					return "0 " + index[0];
				}

				long bytes = Math.Abs(fileSize);
				int loc = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
				double num = Math.Round(bytes / Math.Pow(1024, loc), 1);
				return $"{Math.Sign(fileSize) * num} {index[loc]}";
			}
			catch (Exception)
			{
				throw;
			}
		}

		public FileManagerResponse<FileManagerDirectoryContent> Create(string path, string name, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
			List<FileManagerDirectoryContent> newFolder = new List<FileManagerDirectoryContent>();
			int idValue = dataSource.Select(x => x as FileManagerDirectoryContent).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();

			newFolder.Add(new FileManagerDirectoryContent()
			{
				Id = (idValue + 1).ToString(),
				Name = name,
				Size = 0,
				DateCreated = DateTime.Now,
				DateModified = DateTime.Now,
				Type = "",
				ParentId = (fileDetails[0] as FileManagerDirectoryContent).Id,
				HasChild = false,
				FilterPath = path,
				FilterId = (fileDetails[0] as FileManagerDirectoryContent).FilterId + (fileDetails[0] as FileManagerDirectoryContent).Id + "/",
				IsFile = false,
			});
			response.Files = newFolder;
			dataSource.AddRange(newFolder);
			//dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Id == (fileDetails[0] as FileManagerDirectoryContent).Id).FirstOrDefault().HasChild = true;
			return response;
		}

        public FileManagerResponse<FileManagerDirectoryContent> Search(string path, string searchString, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
            char[] i = new Char[] { '*' };
            FileManagerDirectoryContent[] searchFiles = dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Name.ToLower().Contains(searchString.TrimStart(i).TrimEnd(i).ToLower())).Select(x => x).ToArray();
            response.Files = searchFiles.ToList();
            response.CWD = fileDetails.ToList().First();
            return response;
		}

		public FileManagerResponse<FileManagerDirectoryContent> Rename(string path, string newName, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
			FileManagerDirectoryContent renamedFolder = dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Id == (fileDetails[0] as FileManagerDirectoryContent).Id).FirstOrDefault();
			renamedFolder.Name = newName;
			renamedFolder.DateModified = DateTime.Now;
			response.Files = fileDetails.ToList();
			dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Id == (fileDetails[0] as FileManagerDirectoryContent).Id).FirstOrDefault().Name = newName;
			return response;
		}

		public FileManagerResponse<FileManagerDirectoryContent> Move(string path, FileManagerDirectoryContent targetData, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<FileManagerDirectoryContent> response = new FileManagerResponse<FileManagerDirectoryContent>();
			response.Files = new List<FileManagerDirectoryContent>();
			foreach (FileManagerDirectoryContent file in fileDetails)
			{
				FileManagerDirectoryContent movedFile = dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Name == file.Name).FirstOrDefault();
				movedFile.ParentId = targetData.Id;
				movedFile.FilterPath = targetData.FilterPath + targetData.Name + "/";
				movedFile.FilterId = targetData.FilterId + targetData.Id + "/";
				response.Files.Add(movedFile);
				dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Name == file.Name).FirstOrDefault().ParentId = targetData.Id;
				dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Name == file.Name).FirstOrDefault().FilterPath = targetData.FilterPath + targetData.Name + "/";
				dataSource.Select(x => x as FileManagerDirectoryContent).Where(x => x.Name == file.Name).FirstOrDefault().FilterId = targetData.FilterId + targetData.Id + "/";
			}
			return response;
		}

		public FileManagerResponse<FileManagerDirectoryContent> Copy(string path, FileManagerDirectoryContent targetData, List<FileManagerDirectoryContent> dataSource, FileManagerDirectoryContent[] data)
		{
            FileManagerResponse<FileManagerDirectoryContent> copyResponse = new FileManagerResponse<FileManagerDirectoryContent>();
            List<string> children = dataSource.Where(x => x.ParentId == data[0].Id).Select(x => x.Id).ToList();
            if (children.IndexOf(targetData.Id) != -1 || data[0].Id == targetData.Id)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "The destination folder is the subfolder of the source folder.";
                copyResponse.Error = er;
                return copyResponse;
            }
            foreach (FileManagerDirectoryContent item in data)
            {
                try
                {
                    int idValue = dataSource.Select(x => x as FileManagerDirectoryContent).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
                    if (item.IsFile)
                    {
                        // Copy the file
                        List<FileManagerDirectoryContent> i = dataSource.Where(x => x.Id == item.Id).Select(x => x).ToList();
                        FileManagerDirectoryContent CreateData = new FileManagerDirectoryContent()
                        {
                            Id = (idValue + 1).ToString(),
                            Name = item.Name,
                            Size = i[0].Size,
                            DateCreated = DateTime.Now,
                            DateModified = DateTime.Now,
                            Type = i[0].Type,
                            HasChild = false,
                            ParentId = targetData.Id,
                            IsFile = true,
                            FilterPath = targetData.FilterPath + targetData.Name + "/",
                            FilterId = targetData.FilterId + targetData.Id + "/"
                        };
                        copyFiles.Add(CreateData);
						dataSource.Add(CreateData);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: " + e.Message);
                    return null;
                }
            }
            foreach (FileManagerDirectoryContent item in data)
            {
                try
                {
                    if (!item.IsFile)
                    {
                        this.copyFolderItems(item, targetData, dataSource);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: " + e.Message);
                    return null;
                }
            }
            copyResponse.Files = copyFiles;
            return copyResponse;
        }

        public void copyFolderItems(FileManagerDirectoryContent item, FileManagerDirectoryContent target, List<FileManagerDirectoryContent> dataSource)
        {
            if (!item.IsFile)
            {
                int idVal = dataSource.Select(x => x as FileManagerDirectoryContent).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
                List<FileManagerDirectoryContent> i = dataSource.Where(x => x.Id == item.Id).Select(x => x).ToList();
                FileManagerDirectoryContent CreateData = new FileManagerDirectoryContent()
                {
                    Id = (idVal + 1).ToString(),
                    Name = item.Name,
                    Size = 0,
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now,
                    Type = "folder",
                    HasChild = false,
                    ParentId = target.Id,
                    IsFile = false,
                    FilterPath = target.FilterPath + target.Name + "/",
                    FilterId = target.FilterId + target.Id + "/"
                };
                copyFiles.Add(CreateData);
				dataSource.Add(CreateData);
				if(target.HasChild == false)
				{
					dataSource.Where(x => x.Id == target.Id).Select(x => x).ToList()[0].HasChild = true;
				}
            }
            FileManagerDirectoryContent[] childs = dataSource.Where(x => x.ParentId == item.Id).Select(x => x).ToArray();
            int idValue = dataSource.Select(x => x as FileManagerDirectoryContent).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
            if (childs.Length > 0)
            {
                foreach (FileManagerDirectoryContent child in childs)
                {
                    if (child.IsFile)
                    {
                        int idVal = dataSource.Select(x => x as FileManagerDirectoryContent).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();

                        // Copy the file
                        FileManagerDirectoryContent CreateData = new FileManagerDirectoryContent()
                        {
                            Id = (idVal + 1).ToString(),
                            Name = child.Name,
                            Size = child.Size,
                            DateCreated = DateTime.Now,
                            DateModified = DateTime.Now,
                            Type = child.Type,
                            HasChild = false,
                            ParentId = idValue.ToString(),
                            IsFile = true,
                            FilterPath = target.FilterPath + target.Name + "/",
                            FilterId = target.FilterId + target.Id + "/"
                        };
						dataSource.Add(CreateData);
                    }
                }
                foreach (FileManagerDirectoryContent child in childs)
                {
                    if (!child.IsFile)
                    {
                        this.copyFolderItems(child, dataSource.Where(x => x.Id == (idValue).ToString()).Select(x => x).ToArray()[0], dataSource);
                    }
                }
            }
        }

		public FileStreamResult GetExampleStream()
		{
            String fullPath = basePath;
            FileStream fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
            return fileStreamResult;
        }
    }
}
