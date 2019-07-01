using Syncfusion.EJ2.FileManager.Base;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using Syncfusion.EJ2.FileManager.Base.SQLFileProvider;
using Microsoft.Extensions.Configuration;

namespace EJ2APIServices.Controllers
{

    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class FileManagerController : Controller
    {
        public SQLFileProvider operation;
        public FileManagerController(IConfiguration configuration)
        {
            this.operation = new SQLFileProvider(configuration);
            this.operation.SetSQLConnection("FileManagerConnection", "Product", "0");
        }
        [Route("FileOperations")]
        public object FileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Action == "delete" || args.Action == "rename")
            {
                if ((args.TargetPath == null) && (args.Path == ""))
                {
                    FileManagerResponse response = new FileManagerResponse();
                    ErrorDetails er = new ErrorDetails
                    {
                        Code = "403",
                        Message = "Restricted to modify the root folder."
                    };
                    response.Error = er;
                    return this.operation.ToCamelCase(response);
                }
            }
            switch (args.Action)
            {
                case "read":

                    return this.operation.ToCamelCase(this.operation.GetFiles(args.Path, false, args.Data));
                case "delete":
                    return this.operation.ToCamelCase(this.operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    return this.operation.ToCamelCase(this.operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    return this.operation.ToCamelCase(this.operation.Create(args.Path, args.Name, args.Data));
                case "search":
                    return this.operation.ToCamelCase(this.operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    return this.operation.ToCamelCase(this.operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
                case "move":
                    return this.operation.ToCamelCase(this.operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "copy":
                    return this.operation.ToCamelCase(this.operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
            }
            return null;
        }

        [Route("Upload")]
        public IActionResult Upload(string path, IList<IFormFile> uploadFiles, string action, string data)
        {
            FileManagerResponse uploadResponse;
            FileManagerDirectoryContent[] dataObject = new FileManagerDirectoryContent[1];
            dataObject[0] = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(data);
            uploadResponse = operation.Upload(path, uploadFiles, action, dataObject);
            if (uploadResponse.Error != null)
            {
                Response.Clear();
                Response.ContentType = "application/json; charset=utf-8";
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = uploadResponse.Error.Message;
            }
            return Content("");
        }

        [Route("Download")]
        public IActionResult Download(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            args.Path = (args.Path);
            return operation.Download(args.Path, args.Names, args.Data);
        }


        [Route("GetImage")]
        public IActionResult GetImage(FileManagerDirectoryContent args)
        {
            return this.operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }

}
