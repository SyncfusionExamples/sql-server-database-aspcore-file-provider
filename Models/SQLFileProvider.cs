using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using Syncfusion.EJ2.FileManager.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Win32;
using System.IO.Compression;
using System.Data;

namespace Syncfusion.EJ2.FileManager.Base.SQLFileProvider
{
    public class SQLFileProvider : SQLFileProviderBase
    {                
        string ConnectionString;
        string TableName;
        List<string> deleteFilesId = new List<string>();
        List<string> checkedIDs = new List<string>();
        string RootId;
        SqlConnection con;
        IConfiguration configuration;
        private List<string> Folder = new List<string>();
        private string FolderEntryName = "";
        private string InitEntry = "";
        private string PreviousEntryName = "";

        // Sets the configuration
        public SQLFileProvider(IConfiguration configuration)
        {
            this.configuration = configuration;

        }
        // Initializes the SqlConnection
        public SqlConnection setSQLDBConnection()
        {

            string Path = Environment.CurrentDirectory;
            string[] appPath = Path.Split(new string[] { "bin" }, StringSplitOptions.None);
            this.ConnectionString = this.ConnectionString.Replace("|DataDirectory|", appPath[0]);

            return new SqlConnection(@"" + this.ConnectionString);


        }


        public string ToCamelCase(FileManagerResponse userData)
        {
            return JsonConvert.SerializeObject(userData, new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
        }
        // Sets the SQLConnection string and table name and table id
        public void SetSQLConnection(string name, string tableName, string tableID)
        {
            this.ConnectionString = this.configuration.GetConnectionString(name);
            this.TableName = tableName;
            this.RootId = tableID;
        }
        // Reads the files from SQL table
        public FileManagerResponse GetFiles(string path, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            con = setSQLDBConnection();
            string ParentID = "";
            string IsRoot = "";
            if (path == "/")
            {
                ParentID = this.RootId;
                try
                {
                    con.Open();
                    string parentIDQuery = "select ItemID from " + this.TableName + " where ParentID='" + RootId + "'";
                    SqlCommand cmdd = new SqlCommand(parentIDQuery, con);
                    SqlDataReader reader = cmdd.ExecuteReader();
                    while (reader.Read())
                    {
                        IsRoot = reader["ItemID"].ToString();
                    }

                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    con.Close();

                }

            }
            else
            {
                try
                {
                    con.Open();
                    string parentIDQuery = "select ParentID from " + this.TableName + " where ItemID='" + data[0].Id + "'";
                    SqlCommand cmdd = new SqlCommand(parentIDQuery, con);
                    SqlDataReader reader = cmdd.ExecuteReader();
                    while (reader.Read())
                    {
                        ParentID = reader["ParentID"].ToString();
                    }

                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    con.Close();

                }
            }

            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();

            string query =
           "select * from " + this.TableName + "";
            SqlCommand cmd = new SqlCommand(query, con);
            FileManagerResponse readResponse = new FileManagerResponse();
            try
            {

                SqlConnection con = new SqlConnection(this.ConnectionString);
                string querystring = "";
                if (data.Length == 0)
                {
                    querystring = "select * from " + this.TableName + " where ParentID='" + ParentID + "'";
                }
                else
                {
                    querystring = "select * from " + this.TableName + " where ItemID='" + data[0].Id + "'";
                }
                try
                {
                    con.Open();
                    SqlCommand cmdd = new SqlCommand(querystring, con);
                    SqlDataReader reader = cmdd.ExecuteReader();
                    while (reader.Read())
                    {
                        cwd = new FileManagerDirectoryContent
                        {
                            Name = reader["Name"].ToString().Trim(),
                            Size = (long)reader["Size"],
                            IsFile = (bool)reader["IsFile"],
                            FilterPath = data.Length > 0 ? data[0].FilterPath : "",
                            DateModified = (DateTime)reader["DateModified"],
                            DateCreated = (DateTime)reader["DateCreated"],
                            Type = GetDefaultExtension(reader["MimeType"].ToString()),
                            Id = reader["ItemID"].ToString(),
                            HasChild = (bool)reader["HasChild"]
                        };

                    }

                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    con.Close();

                }

            }
            catch (SqlException e)
            {
                Console.WriteLine("Error Generated. Details: " + e.ToString());
            }
            string FilesQueryString;
            if (path == "/")
            {
                FilesQueryString = " select * from " + this.TableName + " where ParentID = '" + IsRoot + "'";
            }
            else
            {
                FilesQueryString = "select * from " + this.TableName + " where ParentID='" + data[0].Id + "'";
            }

            try
            {
                con.Open();
                SqlCommand cmdd = new SqlCommand(FilesQueryString, con);
                SqlDataReader reader = cmdd.ExecuteReader();
                while (reader.Read())
                {
                    var childFiles = new FileManagerDirectoryContent
                    {
                        Name = reader["Name"].ToString().Trim(),
                        Size = (long)reader["Size"],
                        IsFile = (bool)reader["IsFile"],
                        DateModified = (DateTime)reader["DateModified"],
                        DateCreated = (DateTime)reader["DateCreated"],
                        HasChild = (bool)reader["HasChild"],
                        Type = GetDefaultExtension(reader["MimeType"].ToString()),
                        Id = reader["ItemID"].ToString()
                    };
                    files.Add(childFiles);
                }
                reader.Close();
                foreach (var file in files)
                {
                    file.FilterId = GetFilterId(file.Id);
                    file.FilterPath = data.Length != 0 ? GetFilterPath(file.Id) : "/";
                }

            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                con.Close();

            }
            readResponse.Files = files;
            readResponse.CWD = cwd;
            return readResponse;
        }
        // Creates a NewFolder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse createResponse = new FileManagerResponse();
            try
            {

                FileManagerDirectoryContent CreateData = new FileManagerDirectoryContent();

                con = setSQLDBConnection();
                try
                {
                    con.Open();
                    string updateQuery = "update " + this.TableName + " SET HasChild='True' where ItemID='" + data[0].Id + "'";
                    SqlCommand updatecommand = new SqlCommand(updateQuery, con);
                    updatecommand.ExecuteNonQuery();
                    con.Close();
                    con.Open();
                    string ParentID = null;
                    string parentIDQuery = "select ParentID from " + this.TableName + " where ItemID='" + data[0].Id + "'";
                    SqlCommand cmdd = new SqlCommand(parentIDQuery, con);
                    SqlDataReader RD = cmdd.ExecuteReader();
                    while (RD.Read())
                    {
                        ParentID = RD["ParentID"].ToString();
                    }
                    con.Close();
                    Int32 count;
                    con.Open();
                    SqlCommand Checkcommand = new SqlCommand("select COUNT(Name) from " + this.TableName + " where ParentID='" + data[0].Id + "' AND MimeType= 'folder' AND Name = '" + name.Trim() + "'", con);
                    count = (Int32)Checkcommand.ExecuteScalar();
                    con.Close();
                    if (count != 0)
                    {
                        ErrorDetails er = new ErrorDetails();
                        er.Code = "400";
                        er.Message = "A folder with the name " + name.Trim() + " already exists.";
                        createResponse.Error = er;
                        return createResponse;
                    }
                    else
                    {
                        con.Open();
                        SqlCommand command = new SqlCommand("INSERT INTO " + TableName + " (Name, ParentID, Size, IsFile, MimeType, DateModified, DateCreated, HasChild, IsRoot, Type) VALUES ( @Name, @ParentID, @Size, @IsFile, @MimeType, @DateModified, @DateCreated, @HasChild, @IsRoot, @Type )", con);
                        command.Parameters.Add(new SqlParameter("@Name", name.Trim()));
                        command.Parameters.Add(new SqlParameter("@ParentID", data[0].Id));
                        command.Parameters.Add(new SqlParameter("@Size", 30));
                        command.Parameters.Add(new SqlParameter("@IsFile", false));
                        command.Parameters.Add(new SqlParameter("@MimeType", "Folder"));
                        command.Parameters.Add(new SqlParameter("@DateModified", DateTime.Now));
                        command.Parameters.Add(new SqlParameter("@DateCreated", DateTime.Now));
                        command.Parameters.Add(new SqlParameter("@HasChild", false));
                        command.Parameters.Add(new SqlParameter("@IsRoot", false));
                        command.Parameters.Add(new SqlParameter("@Type", "Folder"));
                        command.ExecuteNonQuery();
                        con.Close();
                        con.Open();
                        SqlCommand readcommand = new SqlCommand("Select * from " + TableName + " where ParentID =" + data[0].Id + " and MimeType = 'folder' and Name ='" + name.Trim() +"'", con);
                        SqlDataReader reader = readcommand.ExecuteReader();
                        while (reader.Read())
                        {

                            CreateData = new FileManagerDirectoryContent
                            {
                                Name = reader["Name"].ToString().Trim(),
                                Id = reader["ItemId"].ToString().Trim(),
                                Size = (long)reader["Size"],
                                IsFile = (bool)reader["IsFile"],
                                DateModified = (DateTime)reader["DateModified"],
                                DateCreated = (DateTime)reader["DateCreated"],
                                Type = "",
                                HasChild = (bool)reader["HasChild"]
                            };

                        }
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    con.Close();

                }


                var newData = new FileManagerDirectoryContent[] { CreateData };
                createResponse.Files = newData;
                return createResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                createResponse.Error = er;

                return createResponse;
            }
        }
        private FileStreamResult fileStreamResult;
        private List<String> files = new List<String> { };
        // Downloads file(s) and folder(s)
        public FileStreamResult Download(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            if (data != null)
            {
                byte[] fileContent;
                con = setSQLDBConnection();
                con.Open();
                foreach (FileManagerDirectoryContent item in data)
                {
                    try
                    {
                        SqlCommand myCommand = new SqlCommand("select * from " + TableName + " where ItemId =" + item.Id, con);
                        SqlDataReader myReader = myCommand.ExecuteReader();
                        while (myReader.Read())
                        {

                            if (File.Exists(Path.Combine(Path.GetTempPath(), item.Name)))
                            {
                                File.Delete(Path.Combine(Path.GetTempPath(), item.Name));
                            }
                            if (item.IsFile)
                            {
                                fileContent = (byte[])myReader["Content"];
                                using (Stream file = File.OpenWrite(Path.Combine(Path.GetTempPath(), item.Name)))
                                {
                                    file.Write(fileContent, 0, fileContent.Length);
                                    if (files.IndexOf(item.Name) == -1)
                                    {
                                        files.Add(item.Name);
                                    }
                                }
                            }
                            else
                            {
                                if (files.IndexOf(item.Name) == -1)
                                {
                                    files.Add(item.Name);
                                }
                            }

                        }
                        myReader.Close();
                    }
                    catch (Exception ex) { throw ex; }
                }
                con.Close();
                if (files.Count == 1 && data[0].IsFile)
                {
                    try
                    {
                        FileStream fileStreamInput = new FileStream(Path.Combine(Path.GetTempPath(), files[0]), FileMode.Open, FileAccess.Read);
                        fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                        fileStreamResult.FileDownloadName = files[0];
                    }
                    catch (Exception ex) { throw ex; }
                }
                else
                {
                    ZipArchiveEntry zipEntry;
                    ZipArchive archive;
                    var tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                    try
                    {
                        using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                        {
                            for (var i = 0; i < files.Count; i++)
                            {
                                bool isFile = false;
                                con.Open();
                                SqlCommand downloadCommand = new SqlCommand("select * from " + TableName + " where Name ='" + files[i] + "'", con);
                                SqlDataReader downloadCommandReader = downloadCommand.ExecuteReader();
                                while (downloadCommandReader.Read())
                                {
                                    isFile = (bool)downloadCommandReader["IsFile"];
                                }
                                con.Close();
                                if (isFile)
                                {
                                    zipEntry = archive.CreateEntryFromFile(Path.GetTempPath() + files[i], files[i], CompressionLevel.Fastest);
                                }
                                else
                                {
                                    con.Open();
                                    this.FolderEntryName = files[i];
                                    this.InitEntry = files[i];
                                    DownloadFolder(archive, files[i], con);
                                    con.Close();
                                }
                            }
                            archive.Dispose();
                            FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                            fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                            fileStreamResult.FileDownloadName = files.Count == 1 ? data[0].Name + ".zip" : "Files.zip";
                            if (File.Exists(Path.Combine(Path.GetTempPath(), "temp.zip"))) ;
                            {
                                File.Delete(Path.Combine(Path.GetTempPath(), "temp.zip"));
                            }
                        }
                    }
                    catch (Exception ex) { throw ex; }
                }
            }
            return fileStreamResult;
        }

        public void DownloadFolder(ZipArchive archive, string FolderName, SqlConnection con)
        {
            ZipArchiveEntry zipEntry;
            byte[] fileContent = null;
            string parentID = "";
            string Name = "";
            bool isFile = false;
            zipEntry = archive.CreateEntry(this.FolderEntryName + "/");
            SqlCommand readCommmand = new SqlCommand("select * from " + TableName + " where Name ='" + FolderName + "'", con);
            SqlDataReader readCommmandReader = readCommmand.ExecuteReader();
            while (readCommmandReader.Read())
            {
                parentID = readCommmandReader["ItemID"].ToString().Trim();
            }
            readCommmandReader.Close();
            SqlCommand downloadReadCommand = new SqlCommand("select * from " + TableName + " where ParentID ='" + parentID + "'", con);
            SqlDataReader downloadReadCommandReader = downloadReadCommand.ExecuteReader();
            while (downloadReadCommandReader.Read())
            {
                Name = downloadReadCommandReader["Name"].ToString().Trim();
                isFile = (bool)downloadReadCommandReader["IsFile"];
                if (isFile)
                {
                    fileContent = (byte[])downloadReadCommandReader["Content"];
                }
                if (isFile)
                {
                    if (System.IO.File.Exists(Path.Combine(Path.GetTempPath(), Name)))
                    {
                        System.IO.File.Delete(Path.Combine(Path.GetTempPath(), Name));
                    }
                    using (var file = System.IO.File.OpenWrite(Path.Combine(Path.GetTempPath(), Name)))
                    {
                        file.Write(fileContent, 0, fileContent.Length);
                        file.Close();
                        zipEntry = archive.CreateEntryFromFile(Path.Combine(Path.GetTempPath(), Name), this.FolderEntryName + "\\" + Name, CompressionLevel.Fastest);
                    }
                    if (System.IO.File.Exists(Path.Combine(Path.GetTempPath(), Name)))
                    {
                        System.IO.File.Delete(Path.Combine(Path.GetTempPath(), Name));
                    }
                }
                else
                {
                    this.Folder.Add(Name);
                }
            }
            downloadReadCommandReader.Close();
            string[] folders = this.Folder != null ? this.Folder.ToArray() : new string[] { };
            this.Folder = new List<string>();
            for (var i = 0; i < folders.Length; i++)
            {
                this.FolderEntryName = (this.InitEntry == FolderName ? FolderName : this.PreviousEntryName) + "/" + folders[i];
                this.PreviousEntryName = this.FolderEntryName;
                DownloadFolder(archive, folders[i], con);
            }
        }
        // Calculates the folder size
        public long getFolderSize(string[] idValue)
        {
            long sizeValue = 0;
            con.Open();
            foreach (var id in idValue)
            {
                this.checkedIDs.Add(id);
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ParentID =" + id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlCommand removeCommand = new SqlCommand(removeQuery, con);
                SqlDataReader removeCommandReader = removeCommand.ExecuteReader();
                while (removeCommandReader.Read())
                {
                    this.checkedIDs.Add(removeCommandReader["ItemID"].ToString());
                }
                removeCommandReader.Close();
            }
            con.Close();
            if (this.checkedIDs.Count > 0)
            {
                con.Open();
                string query = "select Size from " + this.TableName + " where ItemID IN (" + string.Join(", ", this.checkedIDs.Select(f => "'" + f + "'")) + ")";
                SqlCommand getDetailsCommand = new SqlCommand(query, con);
                SqlDataReader getDetailsCommandReader = getDetailsCommand.ExecuteReader();
                while (getDetailsCommandReader.Read())
                {
                    sizeValue = sizeValue + long.Parse((getDetailsCommandReader["Size"]).ToString());
                }
                con.Close();
            }
            this.checkedIDs = null;
            return sizeValue;
        }
        // Gets the details of the file(s) or folder(s)
        public FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            con = setSQLDBConnection();
            string rootDirectory = "";
            bool isVariousFolders = false;
            string previousPath = "";
            bool isRoot = false;
            string previousName = "";
            FileManagerResponse getDetailResponse = new FileManagerResponse();
            FileDetails detailFiles = new FileDetails();
            string querystring;
            bool isNamesAvailable = names.Length > 0 ? true : false;
            if (data[0].Id == null)
            {
                querystring = "select * from " + this.TableName + " where ItemID='" + this.RootId + "'";
            }
            else
            {
                querystring = "select * from " + this.TableName + " where ItemID='" + data[0].Id + "'";
            }

            try
            {
                string sizeValue = "";
                var listOfStrings = new List<string>();
                long size = 0;
                long folderValue = 0;
                if (names.Length == 0 && data.Length != 0)
                {
                    List<string> values = new List<string>();
                    foreach (var item in data)
                    {
                        values.Add(item.Name);
                    }
                    names = values.ToArray();
                }
                if (!data[0].IsFile && names.Length == 1)
                {
                    string[] idArray = new string[] { data[0].Id };
                    sizeValue = byteConversion(getFolderSize(idArray));
                }
                else
                {
                    foreach (var item in data)
                    {
                        if (!item.IsFile)
                        {
                            listOfStrings.Add(item.Id);
                        }
                    }
                    folderValue = listOfStrings.Count > 0 ? getFolderSize(listOfStrings.ToArray()) : 0;
                }
                con.Open();
                string rootQuery = "select * from " + this.TableName + " where ParentId='" + this.RootId + "'";
                SqlCommand rootQueryCmdd = new SqlCommand(rootQuery, con);
                SqlDataReader rootQueryReader = rootQueryCmdd.ExecuteReader();
                while (rootQueryReader.Read())
                {
                    rootDirectory = rootQueryReader["Name"].ToString().Trim();
                }
                con.Close();
                con.Open();
                SqlCommand cmdd = new SqlCommand(querystring, con);
                SqlDataReader reader = cmdd.ExecuteReader();
                
                    if (names.Length == 1)
                    {
                    string detailsID = "";
                    while (reader.Read())
                    {
                        detailFiles = new FileDetails
                        {
                            Name = reader["Name"].ToString().Trim(),
                            IsFile = (bool)reader["IsFile"],
                            Size = (bool)reader["IsFile"] ? byteConversion(long.Parse((reader["Size"]).ToString())) : sizeValue,
                            Modified = (DateTime)reader["DateModified"],
                            Created = (DateTime)reader["DateCreated"],
                        };
                        detailsID = reader["ItemID"].ToString().Trim();
                        isRoot = reader["ParentID"].ToString().Trim() == this.RootId;
                    }
                    reader.Close();
                    detailFiles.Location = (rootDirectory + (!isRoot ? (GetFilterPath(detailsID) + detailFiles.Name) : "")).Replace("/",@"\");

                    }
                    else
                    {
                        detailFiles = new FileDetails();
                        foreach (var item in data)
                        {
                            detailFiles.Name = previousName == "" ? previousName = item.Name : previousName + ", " + item.Name;
                            previousPath = previousPath == "" ? item.FilterPath : previousPath;
                            if (previousPath == rootDirectory + item.FilterPath && !isVariousFolders)
                            {
                                previousPath = rootDirectory + item.FilterPath;
                                detailFiles.Location = rootDirectory + (item.FilterPath).Replace("/", @"\");
                            }
                            else
                            {
                                isVariousFolders = true;
                                detailFiles.Location = "Various Folders";
                            }
                            if (item.IsFile)
                            {
                                size = size + item.Size;
                            }
                        }
                        long updatedLongIntValue = size + folderValue;
                        sizeValue = byteConversion(updatedLongIntValue);

                        detailFiles.Size = sizeValue;
                        detailFiles.MultipleFiles = true;
                    }
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                con.Close();

            }

            getDetailResponse.Details = detailFiles;
            return getDetailResponse;
        }
        // Gets the file type
        public static string GetDefaultExtension(string mimeType)
        {
            string result;
            RegistryKey key;
            object value;
            key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType.Trim(), false);
            value = key != null ? key.GetValue("Extension", null) : null;
            result = value != null ? value.ToString() : string.Empty;

            return result;
        }
        // Converts the byte value to the appropriate size value
        public String byteConversion(long fileSize)
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
                return (Math.Sign(fileSize) * num).ToString() + " " + index[loc];
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        // Returns the image
        public FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            con = setSQLDBConnection();
            FileStreamResult fileStreamResult;
            byte[] fileContent;
            con.Open();
            SqlCommand myCommand = new SqlCommand("select * from " + TableName + " where ItemID =" + id, con);
            SqlDataReader myReader = myCommand.ExecuteReader();
            while (myReader.Read())
            {
                fileContent = (byte[])myReader["Content"];
                string name = myReader["Name"].ToString().Trim();
                if (File.Exists(Path.Combine(Path.GetTempPath(), name)))
                {
                    File.Delete(Path.Combine(Path.GetTempPath(), name));
                }
                using (Stream file = File.OpenWrite(Path.Combine(Path.GetTempPath(), name)))
                {
                    file.Write(fileContent, 0, fileContent.Length);
                }
                try
                {
                    FileStream fileStreamInput = new FileStream(Path.Combine(Path.GetTempPath(), name), FileMode.Open, FileAccess.Read);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    return fileStreamResult;
                }
                catch (Exception ex) { throw ex; }
            }
            con.Close();
            return null;
        }
        // Deletes the file(s) or folder(s)
        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse remvoeResponse = new FileManagerResponse();
            string ParentID = "";
            try
            {
                FileManagerDirectoryContent DeletedData = new FileManagerDirectoryContent();
                List<FileManagerDirectoryContent> newData = new List<FileManagerDirectoryContent>();

                con = setSQLDBConnection();
                foreach (var file in data)
                {
                    try
                    {
                        con.Open();
                        string parentIDQuery = "select ParentID from " + this.TableName + " where ItemID='" + file.Id + "'";
                        SqlCommand cmdd = new SqlCommand(parentIDQuery, con);
                        SqlDataReader idreader = cmdd.ExecuteReader();
                        while (idreader.Read())
                        {
                            ParentID = idreader["ParentID"].ToString();
                        }

                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        con.Close();

                    }
                    try
                    {
                        Int32 count;
                        con.Open();
                        SqlCommand Checkcommand = new SqlCommand("select COUNT(*) from " + this.TableName + " where ParentID='" + ParentID + "' AND MimeType= 'folder' AND Name <> '" + file.Name + "'", con);
                        count = (Int32)Checkcommand.ExecuteScalar();
                        con.Close();
                        if (count == 0)
                        {
                            con.Open();
                            string updateQuery = "update " + this.TableName + " SET HasChild='False' where itemId='" + ParentID + "'";
                            SqlCommand updatecommand = new SqlCommand(updateQuery, con);
                            updatecommand.ExecuteNonQuery();
                            con.Close();
                        }
                        con.Open();
                        SqlCommand command = new SqlCommand("select * from " + this.TableName + " where ParentID='" + ParentID + "'", con);
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            DeletedData = new FileManagerDirectoryContent
                            {
                                Name = reader["Name"].ToString().Trim(),
                                Size = (long)reader["Size"],
                                IsFile = (bool)reader["IsFile"],
                                DateModified = (DateTime)reader["DateModified"],
                                DateCreated = (DateTime)reader["DateCreated"],
                                Type = "",
                                HasChild = (bool)reader["HasChild"],
                                Id = reader["ItemID"].ToString()
                            };
                        }
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        con.Close();

                    }

                    try
                    {
                        con.Open();
                        SqlCommand DelCmd = new SqlCommand("delete  from " + this.TableName + " where ItemID='" + file.Id + "'", con);
                        DelCmd.ExecuteNonQuery();
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        con.Close();

                    }
                    newData.Add(DeletedData);
                    remvoeResponse.Files = newData;
                }
                con.Open();
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlCommand removeCommand = new SqlCommand(removeQuery, con);
                SqlDataReader removeCommandReader = removeCommand.ExecuteReader();
                while (removeCommandReader.Read())
                {
                    this.deleteFilesId.Add(removeCommandReader["ItemID"].ToString());
                }
                con.Close();
                con.Open();
                string query = "delete from " + this.TableName + " where ItemID IN (" + string.Join(", ", this.deleteFilesId.Select(f => "'" + f + "'")) + ")";
                SqlCommand updateTableCommand = new SqlCommand(query, con);
                SqlDataReader getDetailsCommandReader = updateTableCommand.ExecuteReader();
                con.Close();
                this.deleteFilesId = null;
                return remvoeResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                remvoeResponse.Error = er;

                return remvoeResponse;
            }
        }
        // Uploads the files
        public virtual FileManagerResponse Upload(string path, IList<IFormFile> uploadFiles, string action, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse uploadResponse = new FileManagerResponse();
            string filename = Path.GetFileName(uploadFiles[0].FileName);
            string contentType = uploadFiles[0].ContentType;
            try
            {

                using (FileStream fsSource = new FileStream(Path.Combine(Path.GetTempPath(), filename), FileMode.Create))
                {
                    uploadFiles[0].CopyTo(fsSource);
                    fsSource.Close();
                }
                using (FileStream fsSource = new FileStream(Path.Combine(Path.GetTempPath(), filename), FileMode.Open, FileAccess.Read))
                {
                    BinaryReader br = new BinaryReader(fsSource);
                    long numBytes = new FileInfo(Path.Combine(Path.GetTempPath(), filename)).Length;
                    byte[] bytes = br.ReadBytes((int)numBytes);
                    UploadQuery(filename, contentType, bytes, data[0].Id);

                }
            }
            catch (FileNotFoundException e)
            {

            }
            return uploadResponse;
        }
        // Updates the data table after uploading the file
        public void UploadQuery(string filename, string contentType, byte[] bytes, string parentId)
        {
            con = setSQLDBConnection();
            con.Open();
            SqlCommand command = new SqlCommand("INSERT INTO " + TableName + " (Name, ParentID, Size, IsFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type) VALUES ( @Name, @ParentID, @Size, @IsFile, @MimeType, @Content, @DateModified, @DateCreated, @HasChild, @IsRoot, @Type )", con);
            command.Parameters.Add(new SqlParameter("@Name", filename));
            command.Parameters.Add(new SqlParameter("@IsFile", true));
            command.Parameters.Add(new SqlParameter("@Size", 20));
            command.Parameters.Add(new SqlParameter("@ParentId", parentId));
            command.Parameters.Add(new SqlParameter("@MimeType", contentType));
            command.Parameters.Add("@Content", SqlDbType.VarBinary).Value = bytes;
            command.Parameters.Add(new SqlParameter("@DateModified", DateTime.Now));
            command.Parameters.Add(new SqlParameter("@DateCreated", DateTime.Now));
            command.Parameters.Add(new SqlParameter("@HasChild", false));
            command.Parameters.Add(new SqlParameter("@IsRoot", false));
            command.Parameters.Add(new SqlParameter("@Type", "File"));
            command.ExecuteNonQuery();
        }
        // Converts the file  to byte array
        public byte[] FileToByteArray(string fileName)
        {
            byte[] fileData = null;

            using (FileStream fs = File.OpenRead(fileName))
            {
                using (BinaryReader binaryReader = new BinaryReader(fs))
                {
                    fileData = binaryReader.ReadBytes((int)fs.Length);
                }
            }
            return fileData;
        }
        // Renames a file or folder
        public FileManagerResponse Rename(string path, string name, string newName, bool replace = false, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse renameResponse = new FileManagerResponse();
            try
            {
                FileManagerDirectoryContent renameData = new FileManagerDirectoryContent();
                con = setSQLDBConnection();
                try
                {
                    con.Open();
                    string updateQuery = "update " + this.TableName + " set Name='" + newName + "' , DateModified='" + DateTime.Now.ToString() + "' where ItemID ='" + data[0].Id + "'";
                    SqlCommand updatecommand = new SqlCommand(updateQuery, con);
                    updatecommand.ExecuteNonQuery();
                    con.Close();
                    try
                    {
                        con.Open();
                        string querystring = "select * from " + this.TableName + " where ItemID='" + data[0].Id + "'";
                        SqlCommand cmdd = new SqlCommand(querystring, con);
                        SqlDataReader reader = cmdd.ExecuteReader();
                        while (reader.Read())
                        {
                            renameData = new FileManagerDirectoryContent
                            {
                                Name = reader["Name"].ToString().Trim(),
                                Id = reader["ItemID"].ToString().Trim(),
                                Size = (long)reader["Size"],
                                FilterPath = data[0].FilterPath,
                                IsFile = (bool)reader["IsFile"],
                                DateModified = (DateTime)reader["DateModified"],
                                DateCreated = (DateTime)reader["DateCreated"],
                                Type = "",
                                HasChild = (bool)reader["HasChild"]
                            };

                        }
                        reader.Close();
                        renameData.FilterId = GetFilterId(renameData.Id);
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        con.Close();
                    }

                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    con.Close();

                }
                var newData = new FileManagerDirectoryContent[] { renameData };
                renameResponse.Files = newData;
                return renameResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                renameResponse.Error = er;

                return renameResponse;
            }
        }

        public string GetFilterPath(string id)
        {
            List<string> Parents = new List<string>();
            string querystring = "select ParentID from " + this.TableName + " where ItemID='" + id + "'";
            SqlCommand cmdd = new SqlCommand(querystring, con);
            string IdValue = cmdd.ExecuteScalar().ToString().Trim();
            string query = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ItemID =" + IdValue + " union all select p.ItemID, p.Name, p.ParentID from " + this.TableName + " p inner join cte on cte.ParentID = p.ItemID) select Name from cte where ParentID != 0";
            SqlCommand queryCommand = new SqlCommand(query, con);
            SqlDataReader reader = queryCommand.ExecuteReader();
            while (reader.Read())
            {
                Parents.Add(reader["Name"].ToString().Trim());
            }
            reader.Close();
            return ("/" + (Parents.Count > 0 ? (string.Join("/", Parents.ToArray().Reverse()) + "/") : ""));
        }

        public string GetFilterId(string id)
        {
            List<string> Parents = new List<string>();
            string querystring = "select ParentID from " + this.TableName + " where ItemID='" + id + "'";
            SqlCommand cmdd = new SqlCommand(querystring, con);
            string IdValue = cmdd.ExecuteScalar().ToString().Trim();
            string query = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ItemID =" + IdValue + " union all select p.ItemID, p.Name, p.ParentID from " + this.TableName + " p inner join cte on cte.ParentID = p.ItemID) select ItemID from cte";
            SqlCommand queryCommand = new SqlCommand(query, con);
            SqlDataReader reader = queryCommand.ExecuteReader();
            while (reader.Read())
            {
                Parents.Add(reader["ItemID"].ToString().Trim());
            }
            reader.Close();
            return (string.Join("/", Parents.ToArray().Reverse()) + "/");
        }
        // Search for file(s) or folder(s)
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {
            con = setSQLDBConnection();
            FileManagerResponse searchResponse = new FileManagerResponse();
            try
            {
                if (path == null) { path = string.Empty; };

                var searchWord = searchString;
                FileManagerDirectoryContent searchData;
                FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
                cwd.Name = data[0].Name;
                cwd.Size = data[0].Size;
                cwd.IsFile = false;
                cwd.DateModified = data[0].DateModified;
                cwd.DateCreated = data[0].DateCreated;
                cwd.HasChild = data[0].HasChild;
                cwd.Type = data[0].Type;
                con.Open();
                cwd.FilterPath = GetFilterPath(data[0].Id);
                con.Close();
                searchResponse.CWD = cwd;
                List<FileManagerDirectoryContent> foundedFiles = new List<FileManagerDirectoryContent>();
                List<string> availableFiles = new List<string>();
                con.Open();
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlCommand childCommand = new SqlCommand(removeQuery, con);
                SqlDataReader childCommandReader = childCommand.ExecuteReader();
                while (childCommandReader.Read())
                {
                    availableFiles.Add(childCommandReader["ItemID"].ToString());
                }
                con.Close();
                if (availableFiles.Count > 0)
                {
                    con.Open();
                    SqlCommand searchCommand = new SqlCommand("select * from " + this.TableName + " where Name like '" + searchString.Replace("*", "%") + "' AND ItemID IN(" + string.Join(", ", availableFiles.Select(f => "'" + f + "'")) + ")", con);
                    SqlDataReader reader = searchCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        searchData = new FileManagerDirectoryContent
                        {
                            Name = reader["Name"].ToString().Trim(),
                            Size = (long)reader["Size"],
                            IsFile = (bool)reader["IsFile"],
                            DateModified = (DateTime)reader["DateModified"],
                            DateCreated = (DateTime)reader["DateCreated"],
                            Type = GetDefaultExtension(reader["MimeType"].ToString()),
                            HasChild = (bool)reader["HasChild"],
                            Id = reader["ItemId"].ToString().Trim()
                        };
                        if (searchData.Name != "Products") foundedFiles.Add(searchData);
                    }
                    reader.Close();
                    foreach (var file in foundedFiles)
                    {
                        file.FilterPath = GetFilterPath(file.Id);
                        file.FilterId = GetFilterId(file.Id);
                    }
                }
                searchResponse.Files = (IEnumerable<FileManagerDirectoryContent>)foundedFiles;
                return searchResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                searchResponse.Error = er;

                return searchResponse;
            }
            finally
            {
                con.Close();
            }
        }

        public void CopyFolderFiles(string[] fileID, string[] newTargetID, SqlConnection con)
        {
            List<string> FoldersID = new List<String>();
            List<string> lastInsertedItemId = new List<String>();
            List<string> FromFoldersID = new List<string>();
            List<string> ToFoldersID = new List<string>();
            for (var i = 0; i < fileID.Length; i++)
            {
                string copyQuery = "insert into " + TableName + " (Name, ParentID, Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath) select Name," + newTargetID[i] + " , Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath from " + TableName + " where ParentID = " + fileID[i];
                SqlCommand copyQuerycommand = new SqlCommand(copyQuery, con);
                copyQuerycommand.ExecuteNonQuery();
                string checkingQuery = "Select * from " + TableName + " where ParentID =" + newTargetID[i] + " and MimeType = 'folder'";
                SqlCommand checkingQuerycommand = new SqlCommand(checkingQuery, con);
                SqlDataReader checkingQuerycommandReader = checkingQuerycommand.ExecuteReader();
                while (checkingQuerycommandReader.Read())
                {
                    ToFoldersID.Add(checkingQuerycommandReader["ItemID"].ToString().Trim());
                }
                checkingQuerycommandReader.Close();
                string tocheckingQuery = "Select * from " + TableName + " where ParentID =" + fileID[i] + " and MimeType = 'folder'";
                SqlCommand tocheckingQuerycommand = new SqlCommand(tocheckingQuery, con);
                SqlDataReader tocheckingQuerycommandReader = tocheckingQuerycommand.ExecuteReader();
                while (tocheckingQuerycommandReader.Read())
                {
                    FromFoldersID.Add(tocheckingQuerycommandReader["ItemID"].ToString().Trim());
                }
                tocheckingQuerycommandReader.Close();
            }
            if (FromFoldersID.Count > 0)
            {
                CopyFolderFiles(FromFoldersID.ToArray(), ToFoldersID.ToArray(), con);
            }
        }

        public string GetLastInsertedValue()
        {
            string IDValue;
            string getIDQuery = "SELECT SCOPE_IDENTITY()";
            SqlCommand copyQuerycommand = new SqlCommand(getIDQuery, con);
            IDValue = copyQuerycommand.ExecuteScalar().ToString().Trim();
            return IDValue;
        }
        public FileManagerResponse Copy(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent TargetData, params FileManagerDirectoryContent[] data)
        {
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            List<string> checkingId = new List<string>();
            con = setSQLDBConnection();
            FileManagerResponse copyResponse = new FileManagerResponse();
            con.Open();
            string checkingQuery = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
            SqlCommand copyCheckCommand = new SqlCommand(checkingQuery, con);
            SqlDataReader copyCheckCommandReader = copyCheckCommand.ExecuteReader();
            while (copyCheckCommandReader.Read())
            {
                checkingId.Add(copyCheckCommandReader["ItemID"].ToString());
            }
            con.Close();
            if (checkingId.IndexOf(TargetData.Id) != -1)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "The destination folder is the subfolder of the source folder.";
                copyResponse.Error = er;
                return copyResponse;
            }
            foreach (var item in data)
            {

                try
                {
                    con.Open();
                    List<string> FoldersID = new List<String>();
                    List<string> lastInsertedItemId = new List<String>();
                    string lastID = "";
                    string copyQuery = "insert into " + TableName + " (Name, ParentID, Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath) select Name," + TargetData.Id + " , Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath from " + TableName + " where ItemID = " + item.Id;
                    SqlCommand copyQuerycommand = new SqlCommand(copyQuery, con);
                    copyQuerycommand.ExecuteNonQuery();
                    lastID = GetLastInsertedValue();
                    con.Close();
                    con.Open();
                    string detailsQuery = "Select * from " + this.TableName + " where ItemID=" + item.Id;
                    SqlCommand cmdd = new SqlCommand(detailsQuery, con);
                    SqlDataReader reader = cmdd.ExecuteReader();
                    while (reader.Read())
                    {
                        var copyFiles = new FileManagerDirectoryContent
                        {
                            Name = reader["Name"].ToString().Trim(),
                            Size = (long)reader["Size"],
                            IsFile = (bool)reader["IsFile"],
                            DateModified = DateTime.Now,
                            DateCreated = DateTime.Now,
                            HasChild = (bool)reader["HasChild"],
                            FilterPath = TargetData.FilterPath + "/" + TargetData.Name + "/",
                            Type = GetDefaultExtension(reader["MimeType"].ToString()),
                            Id = reader["ItemID"].ToString()
                        };
                        if (!copyFiles.IsFile)
                        {
                            FoldersID.Add(copyFiles.Id);
                            lastInsertedItemId.Add(lastID);
                        }
                        files.Add(copyFiles);
                    }
                    reader.Close();
                    foreach (var file in files)
                    {
                        file.FilterId = GetFilterId(file.Id);
                    }
                    if (FoldersID.Count > 0)
                    {
                        CopyFolderFiles(FoldersID.ToArray(), lastInsertedItemId.ToArray(), con);
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    con.Close();
                }

            }
            copyResponse.Files = files;
            return copyResponse;
        }

        public FileManagerResponse Move(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent TargetData, params FileManagerDirectoryContent[] data)
        {
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            con = setSQLDBConnection();
            FileManagerResponse moveResponse = new FileManagerResponse();
            List<string> checkingId = new List<string>();
            con.Open();
            string checkingQuery = "with cte as (select ItemID, Name, ParentID from " + this.TableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
            SqlCommand moveCheckCommand = new SqlCommand(checkingQuery, con);
            SqlDataReader moveCheckCommandReader = moveCheckCommand.ExecuteReader();
            while (moveCheckCommandReader.Read())
            {
                checkingId.Add(moveCheckCommandReader["ItemID"].ToString());
            }
            con.Close();
            if (checkingId.IndexOf(TargetData.Id) != -1)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "The destination folder is the subfolder of the source folder.";
                moveResponse.Error = er;
                return moveResponse;
            }
            foreach (var item in data)
            {
                con.Open();
                try
                {
                    string moveQuery = "update " + this.TableName + " SET ParentID='" + TargetData.Id + "' where ItemID='" + item.Id + "'";
                    SqlCommand moveQuerycommand = new SqlCommand(moveQuery, con);
                    moveQuerycommand.ExecuteNonQuery();
                    con.Close();
                    con.Open();
                    string detailsQuery = "Select * from " + this.TableName + " where ItemID=" + item.Id;
                    SqlCommand cmdd = new SqlCommand(detailsQuery, con);
                    SqlDataReader reader = cmdd.ExecuteReader();
                    while (reader.Read())
                    {
                        var moveFiles = new FileManagerDirectoryContent
                        {
                            Name = reader["Name"].ToString().Trim(),
                            Size = (long)reader["Size"],
                            IsFile = (bool)reader["IsFile"],
                            DateModified = DateTime.Now,
                            DateCreated = DateTime.Now,
                            HasChild = (bool)reader["HasChild"],
                            FilterPath = TargetData.FilterPath + "/" + TargetData.Name + "/",
                            Type = GetDefaultExtension(reader["MimeType"].ToString()),
                            Id = reader["ItemID"].ToString()
                        };
                        files.Add(moveFiles);
                    }
                    reader.Close();
                    foreach (var file in files)
                    {
                        file.FilterId = GetFilterId(file.Id);
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    con.Close();
                }

            }
            moveResponse.Files = files;
            return moveResponse;
        }
    }
}

