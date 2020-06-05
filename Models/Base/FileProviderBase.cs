using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace Syncfusion.EJ2.FileManager.Base
{
    public interface FileProviderBase
    {

        FileManagerResponse GetFiles(string path, bool showHiddenItems, params FileManagerDirectoryContent[] data);

        FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] data);

        FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data);

        FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data);

        FileManagerResponse Rename(string path, string name, string newName, bool replace = false, params FileManagerDirectoryContent[] data);

        FileManagerResponse Copy(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data);

        FileManagerResponse Move(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data);

        FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data);

        FileStreamResult Download(string path, string[] names, params FileManagerDirectoryContent[] data);

        FileManagerResponse Upload(string path, IList<IFormFile> uploadFiles, string action, params FileManagerDirectoryContent[] data);

        FileStreamResult GetImage(string path, string parentId, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data);

    }

}





