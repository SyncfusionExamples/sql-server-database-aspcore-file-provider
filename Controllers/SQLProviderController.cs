using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Syncfusion.EJ2.FileManager.Base;
using Syncfusion.EJ2.FileManager.Base.SQLFileProvider;


namespace EJ2APIServices.Controllers
{
    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class SQLProviderController : Controller
    {
        SQLFileProvider operation;
        public SQLProviderController(IConfiguration configuration)
        {
            operation = new SQLFileProvider(configuration);
            string connectionName = "FileManagerConnection";
            string tableName = "Product";
            string rootFolderID = "0";

            // Validate connection name
            if (!Regex.IsMatch(connectionName, "^[a-zA-Z0-9_]*$"))
            {
                throw new ArgumentException("Invalid connection name");
            }

            // Validate table name
            if (!Regex.IsMatch(tableName, "^[a-zA-Z0-9_]*$"))
            {
                throw new ArgumentException("Invalid table name");
            }

            // Validate root folder ID
            if (!Regex.IsMatch(rootFolderID, "^[0-9]*$"))
            {
                throw new ArgumentException("Invalid root folder ID");
            }

            //To configure the database connection, set the connection name, table name and root folder ID value by passing these values to the SetSQLConnection method.
            operation.SetSQLConnection(connectionName, tableName, rootFolderID);
        }
        [Route("SQLFileOperations")]
        public object SQLFileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if ((args.Action == "delete" || args.Action == "rename") && ((args.TargetPath == null) && (args.Path == "")))
            {
                FileManagerResponse response = new FileManagerResponse();
                response.Error = new ErrorDetails { Code = "403", Message = "Restricted to modify the root folder." };
                return operation.ToCamelCase(response);
            }

            switch (args.Action)
            {
                case "read":
                    // Reads the file(s) or folder(s) from the given path.
                    return operation.ToCamelCase(operation.GetFiles(args.Path, false, args.Data));
                case "delete":
                    // Deletes the selected file(s) or folder(s) from the given path.
                    return operation.ToCamelCase(operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    // Gets the details of the selected file(s) or folder(s).
                    return operation.ToCamelCase(operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    // Creates a new folder in a given path.
                    return operation.ToCamelCase(operation.Create(args.Path, args.Name, args.Data));
                case "search":
                    // Gets the list of file(s) or folder(s) from a given path based on the searched key string.
                    return operation.ToCamelCase(operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    // Renames a file or folder.
                    return operation.ToCamelCase(operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
                case "move":
                    // Cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return operation.ToCamelCase(operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "copy":
                    // Copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return operation.ToCamelCase(operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
            }
            return null;
        }

        // Uploads the file(s) into a specified path
        [Route("SQLUpload")]
        public IActionResult SQLUpload(string path, IList<IFormFile> uploadFiles, string action, string data)
        {
            FileManagerResponse uploadResponse;
            FileManagerDirectoryContent[] dataObject = new FileManagerDirectoryContent[1];
            dataObject[0] = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(data);
            uploadResponse = operation.Upload(path, uploadFiles, action, dataObject);
            if (uploadResponse.Error != null)
            {
                Response.Clear();
                Response.ContentType = "application/json; charset=utf-8";
                Response.StatusCode = Convert.ToInt32(uploadResponse.Error.Code);
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = uploadResponse.Error.Message;
            }
            return Content("");
        }

        // Downloads the selected file(s) and folder(s)
        [Route("SQLDownload")]
        public IActionResult SQLDownload(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            args.Path = (args.Path);
            return operation.Download(args.Path, args.Names, args.Data);
        }

        // Gets the image(s) from the given path
        [Route("SQLGetImage")]
        public IActionResult SQLGetImage(FileManagerDirectoryContent args)
        {
            return operation.GetImage(args.Path, args.ParentID, args.Id, true, null, args.Data);
        }
    }

}
