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
        List<CustomFileManagerDirectoryContent> copyFiles = new List<CustomFileManagerDirectoryContent>();

        public async Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Read(string path, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			if (path == "/")
			{
				string ParentId = dataSource
					.Where(x => x.FilterPath == string.Empty)
					.Select(x => x.Id).First();
				response.CWD = dataSource
					.Where(x => x.FilterPath == string.Empty).First();
				response.Files = dataSource
					.Where(x => x.ParentId == ParentId).ToList();
			}
			else
			{
				var id = fileDetails.Count > 0 && fileDetails[0] != null ? fileDetails[0].Id : dataSource
					.Where(x => x.FilterPath == path)
					.Select(x => x.ParentId).First();
				response.CWD = dataSource
					.Where(x => x.Id == (fileDetails.Count > 0 && fileDetails[0] != null ? fileDetails[0].Id : id)).First();
				response.Files = dataSource
					.Where(x => x.ParentId == (fileDetails.Count > 0 && fileDetails[0] != null ? fileDetails[0].Id : id)).ToList();
			}
			return await Task.FromResult(response);
		}
		public Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Delete(string path, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			var idsToDelete = fileDetails.Cast<CustomFileManagerDirectoryContent>().Select(x => x.Id).ToList();
			idsToDelete.AddRange(dataSource.Where(file => idsToDelete.Contains((file).ParentId)).Select(file => (file).Id));
			dataSource.RemoveAll(file => idsToDelete.Contains((file).Id));
			response.Files = fileDetails.ToList();
			return Task.FromResult(response);
		}

		public Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Details(string path, List<CustomFileManagerDirectoryContent> dataSource, CustomFileManagerDirectoryContent[] fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			string RootDirectoryName = dataSource
				.Where(x => x.FilterPath == string.Empty)
				.Select(x => x.Name).First();
			FileDetails Details = new FileDetails();
			if (fileDetails.Length == 0 || fileDetails.Length == 1)
			{
				Details.Created = (fileDetails[0]).DateCreated.ToString();
				Details.IsFile = (fileDetails[0]).IsFile;
				Details.Location = RootDirectoryName == (fileDetails[0]).Name ? RootDirectoryName : RootDirectoryName + (fileDetails[0]).FilterPath + (fileDetails[0]).Name;
				Details.Modified = (fileDetails[0]).DateModified.ToString();
				Details.Name = (fileDetails[0]).Name;
				Details.Permission = (fileDetails[0]).Permission;
				Details.Size = byteConversion((fileDetails[0]).Size);

			}
			else
			{
				string previousName = string.Empty;
				Details.Size = "0";
				for (int i = 0; i < fileDetails.Length; i++)
				{
					Details.Name = string.IsNullOrEmpty(previousName) ? previousName = (fileDetails[i]).Name : previousName = previousName + ", " + (fileDetails[i]).Name; ;
					Details.Size = long.Parse(Details.Size) + (fileDetails[i]).Size + "";
					Details.MultipleFiles = true;
				}
				Details.Size = byteConversion(long.Parse(Details.Size));
			}
			response.Details = Details;
			return Task.FromResult(response);
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

		public async Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Create(string path, string name, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			List<CustomFileManagerDirectoryContent> newFolder = new List<CustomFileManagerDirectoryContent>();
			int idValue = dataSource.Select(x => x).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
            await Task.Delay(1000);
            newFolder.Add(new CustomFileManagerDirectoryContent()
			{
				Id = (idValue + 1).ToString(),
				Name = name,
				Size = 0,
				DateCreated = DateTime.Now,
				DateModified = DateTime.Now,
				Type = "",
				ParentId = (fileDetails[0]).Id,
				HasChild = false,
				FilterPath = path,
				FilterId = (fileDetails[0]).FilterId + (fileDetails[0]).Id + "/",
				IsFile = false,
			});
			response.Files = newFolder;
			dataSource.AddRange(newFolder);
			dataSource.Select(x => x).Where(x => x.Id == (fileDetails[0]).Id).FirstOrDefault().HasChild = true;
			return await Task.FromResult(response);
		}

        public Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Search(string path, string searchString, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
            char[] i = new Char[] { '*' };
            CustomFileManagerDirectoryContent[] searchFiles = dataSource.Select(x => x).Where(x => x.Name.ToLower().Contains(searchString.TrimStart(i).TrimEnd(i).ToLower())).Select(x => x).ToArray();
            response.Files = searchFiles.ToList();
            response.CWD = fileDetails.ToList().First();
            return Task.FromResult(response);
		}

		public Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Rename(string path, string newName, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			CustomFileManagerDirectoryContent renamedFolder = dataSource.Select(x => x).Where(x => x.Id == (fileDetails[0]).Id).FirstOrDefault();
			renamedFolder.Name = newName;
			renamedFolder.DateModified = DateTime.Now;
			response.Files = fileDetails.ToList();
			dataSource.Select(x => x).Where(x => x.Id == (fileDetails[0]).Id).FirstOrDefault().Name = newName;
			return Task.FromResult(response);
		}

		public async Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Move(string path, CustomFileManagerDirectoryContent targetData, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> fileDetails)
		{
			FileManagerResponse<CustomFileManagerDirectoryContent> response = new FileManagerResponse<CustomFileManagerDirectoryContent>();
			response.Files = new List<CustomFileManagerDirectoryContent>();
			foreach (CustomFileManagerDirectoryContent file in fileDetails)
			{
                CustomFileManagerDirectoryContent movedFile = dataSource.Select(x => x).Where(x => x.Name == file.Name).FirstOrDefault();
				movedFile.ParentId = targetData.Id;
				movedFile.FilterPath = targetData.FilterPath.Replace(@"\", "/", StringComparison.Ordinal) + targetData.Name + "/";
				movedFile.FilterId = targetData.FilterId + targetData.Id + "/";
				response.Files.Add(movedFile);
				dataSource.Select(x => x).Where(x => x.Name == file.Name).FirstOrDefault().ParentId = targetData.Id;
				dataSource.Select(x => x).Where(x => x.Name == file.Name).FirstOrDefault().FilterPath = targetData.FilterPath.Replace(@"\", "/", StringComparison.Ordinal) + targetData.Name + "/";
				dataSource.Select(x => x).Where(x => x.Name == file.Name).FirstOrDefault().FilterId = targetData.FilterId + targetData.Id + "/";
			}
            await Task.Delay(2000);
            return await Task.FromResult(response);
		}

		public async Task<FileManagerResponse<CustomFileManagerDirectoryContent>> Copy(string path, CustomFileManagerDirectoryContent targetData, List<CustomFileManagerDirectoryContent> dataSource, List<CustomFileManagerDirectoryContent> data)
		{
            FileManagerResponse<CustomFileManagerDirectoryContent> copyResponse = new FileManagerResponse<CustomFileManagerDirectoryContent>();
            List<string> children = dataSource.Where(x => x.ParentId == data[0].Id).Select(x => x.Id).ToList();
            if (children.IndexOf(targetData.Id) != -1 || data[0].Id == targetData.Id)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "The destination folder is the subfolder of the source folder.";
                copyResponse.Error = er;
                return await Task.FromResult(copyResponse);
            }
            foreach (CustomFileManagerDirectoryContent item in data)
            {
                try
                {
                    int idValue = dataSource.Select(x => x).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
                    if (item.IsFile)
                    {
                        // Copy the file
                        List<CustomFileManagerDirectoryContent> i = dataSource.Where(x => x.Id == item.Id).Select(x => x).ToList();
                        CustomFileManagerDirectoryContent CreateData = new CustomFileManagerDirectoryContent()
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
            foreach (CustomFileManagerDirectoryContent item in data)
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
            await Task.Delay(2000);
            copyResponse.Files = copyFiles;
            return await Task.FromResult(copyResponse);
        }

        public void copyFolderItems(CustomFileManagerDirectoryContent item, CustomFileManagerDirectoryContent target, List<CustomFileManagerDirectoryContent> dataSource)
        {
            if (!item.IsFile)
            {
                int idVal = dataSource.Select(x => x).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
                List<CustomFileManagerDirectoryContent> i = dataSource.Where(x => x.Id == item.Id).Select(x => x).ToList();
                CustomFileManagerDirectoryContent CreateData = new CustomFileManagerDirectoryContent()
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
            CustomFileManagerDirectoryContent[] childs = dataSource.Where(x => x.ParentId == item.Id).Select(x => x).ToArray();
            int idValue = dataSource.Select(x => x).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();
            if (childs.Length > 0)
            {
                foreach (CustomFileManagerDirectoryContent child in childs)
                {
                    if (child.IsFile)
                    {
                        int idVal = dataSource.Select(x => x).Select(x => x.Id).ToArray().Select(int.Parse).ToArray().Max();

                        // Copy the file
                        CustomFileManagerDirectoryContent CreateData = new CustomFileManagerDirectoryContent()
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
                foreach (CustomFileManagerDirectoryContent child in childs)
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
