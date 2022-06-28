﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.IO;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Runtime.InteropServices.WindowsRuntime;


namespace Syncfusion.EJ2.FileManager.Base.SQLFileProvider
{
    public class SQLFileProvider : SQLFileProviderBase
    {
        string connectionString;
        string tableName;
        List<string> deleteFilesId = new List<string>();
        List<string> checkedIDs = new List<string>();
        string rootId;
        SqlConnection sqlConnection;
        IConfiguration configuration;
        private List<string> folder = new List<string>();
        private List<String> files = new List<String> { };
        private string folderEntryName = "";
        private string initEntry = "";
        private string previousEntryName = "";
        AccessDetails AccessDetails = new AccessDetails();
        private string accessMessage = string.Empty;

        // Sets the configuration
        public SQLFileProvider(IConfiguration configuration) { this.configuration = configuration; }
        // Initializes the SqlConnection
        public SqlConnection setSQLDBConnection()
        {
            string[] appPath = (Environment.CurrentDirectory).Split(new string[] { "bin" }, StringSplitOptions.None);
            connectionString = connectionString.Replace("|DataDirectory|", appPath[0]);
            return new SqlConnection(@"" + connectionString);
        }

        // Sets the SQLConnection string, table name and table id
        public void SetSQLConnection(string name, string sqlTableName, string tableID)
        {
            connectionString = configuration.GetConnectionString(name);
            tableName = sqlTableName;
            rootId = tableID;
        }

        public void SetRules(AccessDetails details)
        {
            this.AccessDetails = details;
        }

        // Reads the files from SQL table
        public FileManagerResponse GetFiles(string path, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            sqlConnection = setSQLDBConnection();
            string parentID = "";
            string isRoot = "";
            sqlConnection.Open();
            FileManagerResponse readResponse = new FileManagerResponse();
            try
            {
                if (path == "/")
                {
                    parentID = this.rootId;
                    try
                    {
                        SqlDataReader reader = (new SqlCommand(("select ItemID from " + this.tableName + " where ParentID='" + rootId + "'"), sqlConnection)).ExecuteReader();
                        while (reader.Read()) { isRoot = reader["ItemID"].ToString(); }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                }
                else
                {
                    try
                    {
                        SqlDataReader reader = (new SqlCommand(("select ParentID from " + this.tableName + " where ItemID='" + data[0].Id + "'"), sqlConnection)).ExecuteReader();
                        while (reader.Read()) { parentID = reader["ParentID"].ToString(); }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                }

                FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
                List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
                try
                {

                    SqlConnection sqlConnection = new SqlConnection(this.connectionString);
                    try
                    {
                        sqlConnection.Open();
                        SqlDataReader reader = (new SqlCommand(("select * from " + this.tableName + ((data.Length == 0) ? (" where ParentID='" + parentID) : " where ItemID='" + data[0].Id) + "'"), sqlConnection)).ExecuteReader();
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
                                HasChild = (bool)reader["HasChild"],
                                ParentID = reader["ParentID"].ToString(),
                            };
                            AccessPermission permission = GetPermission(cwd.Id, cwd.ParentID, cwd.Name, cwd.IsFile, path);
                            cwd.Permission = permission;
                        }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                }
                catch (SqlException e) { Console.WriteLine("Error Generated. Details: " + e.ToString()); }
                try
                {
                    sqlConnection.Open();
                    SqlDataReader reader = (new SqlCommand(("select * from " + this.tableName + " where ParentID = '" + ((path == "/") ? isRoot : data[0].Id) + "'"), sqlConnection)).ExecuteReader();
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
                            Id = reader["ItemID"].ToString(),
                            ParentID = reader["ParentID"].ToString(),
                        };

                        AccessPermission permission = GetPermission(childFiles.Id, childFiles.ParentID, childFiles.Name, childFiles.IsFile, path);
                        childFiles.Permission = permission;
                        files.Add(childFiles);
                    }
                    reader.Close();

                    foreach (var file in files)
                    {
                        file.FilterId = GetFilterId(file.Id);
                        file.FilterPath = data.Length != 0 ? GetFilterPath(file.Id) : "/";
                    }

                }
                catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                finally { sqlConnection.Close(); }
                readResponse.Files = files;
                readResponse.CWD = cwd;

                if (cwd.Permission != null && !cwd.Permission.Read)
                {
                    readResponse.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
                return readResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                readResponse.Error = error;
                return readResponse;
            }
        }

        protected AccessPermission GetPermission(string id,  string parentId, string name, bool isFile, string path)
        {
            AccessPermission FilePermission = new AccessPermission();
            if (isFile)
            {
                if (this.AccessDetails.AccessRules == null)
                {
                    return null;
                }
                string nameExtension = Path.GetExtension(name).ToLower();
                AccessRule accessFileRule = new AccessRule();
                foreach (AccessRule fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Id) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (id == fileRule.Id)
                        {
                            FilePermission = UpdateFileRules(FilePermission, fileRule, name);
                        }
                        else if (fileRule.Id.IndexOf("*.*") > -1)
                        {
                            string parentPath = fileRule.Id.Substring(0, fileRule.Id.IndexOf("*.*"));
                            if (parentPath == "")
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule, name);
                            }
                            else
                            {
                                string idValue = parentPath.Substring(0, parentPath.LastIndexOf("/"));
                                bool isAccessId = path.Contains(idValue);
                                if (idValue == parentId || isAccessId)
                                {
                                    accessFileRule = UpdateFilePermission(fileRule, parentPath, id);
                                    FilePermission = UpdateFileRules(FilePermission, accessFileRule, name);
                                }
                            }
                        }
                        else if (fileRule.Id.IndexOf("*.") > -1)
                        {
                            string pathExtension = Path.GetExtension(fileRule.Id).ToLower();
                            string parentPath = fileRule.Id.Substring(0, fileRule.Id.IndexOf("*."));
                            if (parentPath == "")
                            {
                                if (pathExtension == nameExtension)
                                {
                                    FilePermission = UpdateFileRules(FilePermission, fileRule, name);
                                }
                            }
                            else
                            {
                                string idValue = parentPath.Substring(0, parentPath.LastIndexOf("/"));
                                bool isAccessId = path.Contains(idValue);
                                if ((idValue == parentId || isAccessId) && pathExtension == nameExtension)
                                {
                                    accessFileRule = UpdateFilePermission(fileRule, parentPath, id);
                                    FilePermission = UpdateFileRules(FilePermission, accessFileRule, name);
                                }
                            }
                        }
                    }
                }
                return FilePermission;
            }
            else
            {
                AccessRule accessFolderRule = new AccessRule();

                if (this.AccessDetails.AccessRules == null)
                {
                    return null;
                }
                foreach (AccessRule folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Id != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (id == folderRule.Id)
                        {
                            FilePermission = UpdateFolderRules(FilePermission, folderRule, name);
                        }
                        else if (folderRule.Id.IndexOf("*") > -1)
                        {
                            string parentPath = folderRule.Id.Substring(0, folderRule.Id.IndexOf("*"));
                            if (parentPath == "")
                            {
                                FilePermission = UpdateFolderRules(FilePermission, folderRule, name);
                            }
                            else
                            {
                                string idValue = parentPath.Substring(0, parentPath.LastIndexOf("/")); 
                                if (idValue == parentId) 
                                {
                                    accessFolderRule = UpdateFolderPermission(folderRule, parentPath, id);
                                    FilePermission = UpdateFolderRules(FilePermission, accessFolderRule, name);
                                }
                            }
                        }
                        else if (folderRule.Id.IndexOf("/") > -1)
                        {
                            string idValue = folderRule.Id.Substring(0, folderRule.Id.LastIndexOf("/"));
                            bool isAccessId = path.Contains(idValue);
                            if (id == idValue || parentId == idValue || isAccessId)
                            {
                                accessFolderRule = UpdateFolderPermission(folderRule, folderRule.Id, id);
                                FilePermission = UpdateFolderRules(FilePermission, accessFolderRule, name);
                            }
                        }
                    }
                }
                return FilePermission;
            }
        }

        protected AccessRule UpdateFilePermission(AccessRule accessRule, string parentPath, string id)
        {
            AccessRule accessFileRule = new AccessRule();
            accessFileRule.Copy = accessRule.Copy;
            accessFileRule.Download = accessRule.Download;
            accessFileRule.Write = accessRule.Write;
            accessFileRule.Id = parentPath + id;
            accessFileRule.Read = accessRule.Read;
            accessFileRule.Role = accessRule.Role;
            accessFileRule.Message = accessRule.Message;
            return accessFileRule;
        }

        protected AccessRule UpdateFolderPermission(AccessRule accessRule, string parentPath, string id)
        {
            AccessRule accessFolderRule = new AccessRule();
            accessFolderRule.Copy = accessRule.Copy;
            accessFolderRule.Download = accessRule.Download;
            accessFolderRule.Write = accessRule.Write;
            accessFolderRule.WriteContents = accessRule.WriteContents;
            accessFolderRule.Id = parentPath + id;
            accessFolderRule.Read = accessRule.Read;
            accessFolderRule.Role = accessRule.Role;
            accessFolderRule.Upload = accessRule.Upload;
            accessFolderRule.Message = accessRule.Message;
            return accessFolderRule;
        }

        protected virtual AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule, string fileName)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? "'" + fileName + "' is not accessible. You need permission to perform the read action." : fileRule.Message;
            return filePermission;
        }
        protected virtual AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule, string folderName)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? "'" + folderName + "' is not accessible. You need permission to perform the read action." : folderRule.Message;
            return folderPermission;
        }

        protected bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }

        // Creates a new folder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse createResponse = new FileManagerResponse();
            try
            {
                FileManagerDirectoryContent createData = new FileManagerDirectoryContent();

                AccessPermission createPermission = GetPermission(data[0].Id, data[0].ParentID, data[0].Name, data[0].IsFile, path);
                if (createPermission != null && (!createPermission.Read || !createPermission.WriteContents))
                {
                    accessMessage = createPermission.Message;                       
                    throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the writeContents action.");   
                }

                sqlConnection = setSQLDBConnection();
                try
                {
                    sqlConnection.Open();
                    SqlCommand updatecommand = new SqlCommand(("update " + this.tableName + " SET HasChild='True' where ItemID='" + data[0].Id + "'"), sqlConnection);
                    updatecommand.ExecuteNonQuery();
                    sqlConnection.Close();
                    sqlConnection.Open();
                    string ParentID = null;
                    SqlDataReader RD = (new SqlCommand(("select ParentID from " + this.tableName + " where ItemID='" + data[0].Id + "'"), sqlConnection)).ExecuteReader();
                    while (RD.Read()) { ParentID = RD["ParentID"].ToString(); }
                    sqlConnection.Close();
                    Int32 count;
                    sqlConnection.Open();
                    SqlCommand Checkcommand = new SqlCommand("select COUNT(Name) from " + this.tableName + " where ParentID='" + data[0].Id + "' AND MimeType= 'folder' AND Name = '" + name.Trim() + "'", sqlConnection);
                    count = (Int32)Checkcommand.ExecuteScalar();
                    sqlConnection.Close();
                    if (count != 0)
                    {
                        ErrorDetails error = new ErrorDetails();
                        error.Code = "400";
                        error.Message = "A folder with the name " + name.Trim() + " already exists.";
                        createResponse.Error = error;
                        return createResponse;
                    }
                    else
                    {
                        sqlConnection.Open();
                        SqlCommand command = new SqlCommand("INSERT INTO " + tableName + " (Name, ParentID, Size, IsFile, MimeType, DateModified, DateCreated, HasChild, IsRoot, Type) VALUES ( @Name, @ParentID, @Size, @IsFile, @MimeType, @DateModified, @DateCreated, @HasChild, @IsRoot, @Type )", sqlConnection);
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
                        sqlConnection.Close();
                        sqlConnection.Open();
                        SqlCommand readcommand = new SqlCommand("Select * from " + tableName + " where ParentID =" + data[0].Id + " and MimeType = 'folder' and Name ='" + name.Trim() + "'", sqlConnection);
                        SqlDataReader reader = readcommand.ExecuteReader();
                        while (reader.Read())
                        {
                            createData = new FileManagerDirectoryContent
                            {
                                Name = reader["Name"].ToString().Trim(),
                                Id = reader["ItemId"].ToString().Trim(),
                                Size = (long)reader["Size"],
                                IsFile = (bool)reader["IsFile"],
                                DateModified = (DateTime)reader["DateModified"],
                                DateCreated = (DateTime)reader["DateCreated"],
                                Type = "",
                                HasChild = (bool)reader["HasChild"],
                                ParentID = reader["ParentID"].ToString().Trim(),
                            };
                            AccessPermission permission = GetPermission(createData.Id, createData.ParentID, createData.Name, createData.IsFile, path);
                            createData.Permission = permission;
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                finally { sqlConnection.Close(); }
                createResponse.Files = new FileManagerDirectoryContent[] { createData };
                return createResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                createResponse.Error = error;
                return createResponse;
            }
        }

        // Downloads file(s) and folder(s)
        public FileStreamResult Download(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            try
            {
                FileStreamResult fileStreamResult = null;
                if (data != null)
                {
                    sqlConnection = setSQLDBConnection();
                    sqlConnection.Open();
                    foreach (FileManagerDirectoryContent item in data)
                    {
                        bool isFile = item.IsFile;
                        AccessPermission permission = GetPermission(item.Id, item.ParentID, item.Name, isFile, path);
                        if (permission != null && (!permission.Read || !permission.Download))
                        {
                            throw new UnauthorizedAccessException("'" + item.Name + "' is not accessible. Access is denied.");
                        }
                        try
                        {
                            SqlDataReader myReader = (new SqlCommand("select * from " + tableName + " where ItemId =" + item.Id, sqlConnection)).ExecuteReader();
                            while (myReader.Read())
                            {

                                if (File.Exists(Path.Combine(Path.GetTempPath(), item.Name)))
                                    File.Delete(Path.Combine(Path.GetTempPath(), item.Name));
                                if (item.IsFile)
                                {
                                    using (Stream file = File.OpenWrite(Path.Combine(Path.GetTempPath(), item.Name)))
                                    {
                                        file.Write(((byte[])myReader["Content"]), 0, ((byte[])myReader["Content"]).Length);
                                        if (files.IndexOf(item.Name) == -1) files.Add(item.Name);
                                    }
                                }
                                else if (files.IndexOf(item.Name) == -1) files.Add(item.Name);
                            }
                            myReader.Close();
                        }
                        catch (Exception ex) { throw ex; }
                    }
                    sqlConnection.Close();
                    if (files.Count == 1 && data[0].IsFile)
                    {
                        try
                        {
                            fileStreamResult = new FileStreamResult((new FileStream(Path.Combine(Path.GetTempPath(), files[0]), FileMode.Open, FileAccess.Read)), "APPLICATION/octet-stream");
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
                                    sqlConnection.Open();
                                    SqlCommand downloadCommand = new SqlCommand("select * from " + tableName + " where Name ='" + files[i] + "'", sqlConnection);
                                    SqlDataReader downloadCommandReader = downloadCommand.ExecuteReader();
                                    while (downloadCommandReader.Read()) { isFile = (bool)downloadCommandReader["IsFile"]; }
                                    sqlConnection.Close();
                                    if (isFile)
                                        zipEntry = archive.CreateEntryFromFile(Path.GetTempPath() + files[i], files[i], CompressionLevel.Fastest);
                                    else
                                    {
                                        sqlConnection.Open();
                                        this.folderEntryName = files[i];
                                        this.initEntry = files[i];
                                        DownloadFolder(archive, files[i], sqlConnection);
                                        sqlConnection.Close();
                                    }
                                }
                                archive.Dispose();
                                fileStreamResult = new FileStreamResult((new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete)), "APPLICATION/octet-stream");
                                fileStreamResult.FileDownloadName = files.Count == 1 ? data[0].Name + ".zip" : "Files.zip";
                                if (File.Exists(Path.Combine(Path.GetTempPath(), "temp.zip")))
                                    File.Delete(Path.Combine(Path.GetTempPath(), "temp.zip"));
                            }
                        }
                        catch (Exception ex) { throw ex; }
                    }
                }
                return fileStreamResult;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void DownloadFolder(ZipArchive archive, string folderName, SqlConnection sqlConnection)
        {
            ZipArchiveEntry zipEntry;
            byte[] fileContent = null;
            string parentID = "";
            string fileName = "";
            bool isFile = false;
            zipEntry = archive.CreateEntry(this.folderEntryName + "/");
            SqlDataReader readCommmandReader = (new SqlCommand("select * from " + tableName + " where Name ='" + folderName + "'", sqlConnection)).ExecuteReader();
            while (readCommmandReader.Read()) { parentID = readCommmandReader["ItemID"].ToString().Trim(); }
            readCommmandReader.Close();
            SqlDataReader downloadReadCommandReader = (new SqlCommand("select * from " + tableName + " where ParentID ='" + parentID + "'", sqlConnection)).ExecuteReader();
            while (downloadReadCommandReader.Read())
            {
                fileName = downloadReadCommandReader["Name"].ToString().Trim();
                isFile = (bool)downloadReadCommandReader["IsFile"];
                if (isFile) fileContent = (byte[])downloadReadCommandReader["Content"];
                if (isFile)
                {
                    if (System.IO.File.Exists(Path.Combine(Path.GetTempPath(), fileName)))
                        System.IO.File.Delete(Path.Combine(Path.GetTempPath(), fileName));
                    using (var file = System.IO.File.OpenWrite(Path.Combine(Path.GetTempPath(), fileName)))
                    {
                        file.Write(fileContent, 0, fileContent.Length);
                        file.Close();
                        zipEntry = archive.CreateEntryFromFile(Path.Combine(Path.GetTempPath(), fileName), this.folderEntryName + "\\" + fileName, CompressionLevel.Fastest);
                    }
                    if (System.IO.File.Exists(Path.Combine(Path.GetTempPath(), fileName)))
                        System.IO.File.Delete(Path.Combine(Path.GetTempPath(), fileName));
                }
                else this.folder.Add(fileName);
            }
            downloadReadCommandReader.Close();
            string[] folders = this.folder != null ? this.folder.ToArray() : new string[] { };
            this.folder = new List<string>();
            for (var i = 0; i < folders.Length; i++)
            {
                this.previousEntryName = this.folderEntryName = (this.initEntry == folderName ? folderName : this.previousEntryName) + "/" + folders[i];
                DownloadFolder(archive, folders[i], sqlConnection);
            }
        }
        // Calculates the folder size
        public long GetFolderSize(string[] idValue)
        {
            long sizeValue = 0;
            sqlConnection.Open();
            foreach (var id in idValue)
            {
                this.checkedIDs.Add(id);
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ParentID =" + id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlDataReader removeCommandReader = (new SqlCommand(removeQuery, sqlConnection)).ExecuteReader();
                while (removeCommandReader.Read()) { this.checkedIDs.Add(removeCommandReader["ItemID"].ToString()); }
                removeCommandReader.Close();
            }
            sqlConnection.Close();
            if (this.checkedIDs.Count > 0)
            {
                sqlConnection.Open();
                string query = "select Size from " + this.tableName + " where ItemID IN (" + string.Join(", ", this.checkedIDs.Select(f => "'" + f + "'")) + ")";
                SqlDataReader getDetailsCommandReader = (new SqlCommand(query, sqlConnection)).ExecuteReader();
                while (getDetailsCommandReader.Read()) { sizeValue = sizeValue + long.Parse((getDetailsCommandReader["Size"]).ToString()); }
                sqlConnection.Close();
            }
            this.checkedIDs = null;
            return sizeValue;
        }
        // Gets the details of the file(s) or folder(s)
        public FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            sqlConnection = setSQLDBConnection();
            string rootDirectory = "";
            bool isVariousFolders = false;
            string previousPath = "";
            string previousName = "";
            FileManagerResponse getDetailResponse = new FileManagerResponse();
            FileDetails detailFiles = new FileDetails();
            try
            {
                string queryString = "select * from " + this.tableName + " where ItemID='" + (data[0].Id == null ? this.rootId : data[0].Id) + "'";
                bool isNamesAvailable = names.Length > 0 ? true : false;
                try
                {
                    string sizeValue = "";
                    var listOfStrings = new List<string>();
                    long size = 0;
                    long folderValue = 0;
                    if (names.Length == 0 && data.Length != 0)
                    {
                        List<string> values = new List<string>();
                        foreach (var item in data) { values.Add(item.Name); }
                        names = values.ToArray();
                    }
                    if (!data[0].IsFile && names.Length == 1)
                        sizeValue = ByteConversion(GetFolderSize(new string[] { data[0].Id }));
                    else
                    {
                        foreach (var item in data)
                        {
                            if (!item.IsFile)
                                listOfStrings.Add(item.Id);
                        }
                        folderValue = listOfStrings.Count > 0 ? GetFolderSize(listOfStrings.ToArray()) : 0;
                    }
                    sqlConnection.Open();
                    string rootQuery = "select * from " + this.tableName + " where ParentId='" + this.rootId + "'";
                    SqlDataReader rootQueryReader = (new SqlCommand(rootQuery, sqlConnection)).ExecuteReader();
                    while (rootQueryReader.Read()) { rootDirectory = rootQueryReader["Name"].ToString().Trim(); }
                    sqlConnection.Close();
                    sqlConnection.Open();
                    SqlDataReader reader = (new SqlCommand(queryString, sqlConnection)).ExecuteReader();
                    if (names.Length == 1)
                    {
                        string detailsID = "";
                        string detailsParentId = "";
                        string absoluteFilePath = Path.Combine(Path.GetTempPath(), names[0]);
                        while (reader.Read())
                        {
                            detailFiles = new FileDetails
                            {
                                Name = reader["Name"].ToString().Trim(),
                                IsFile = (bool)reader["IsFile"],
                                Size = (bool)reader["IsFile"] ? ByteConversion(long.Parse((reader["Size"]).ToString())) : sizeValue,
                                Modified = (DateTime)reader["DateModified"],
                                Created = (DateTime)reader["DateCreated"]
                            };
                            detailsID = reader["ItemID"].ToString().Trim();
                            detailsParentId = reader["ParentID"].ToString().Trim();
                            AccessPermission permission = GetPermission(detailsID, detailsParentId, detailFiles.Name, detailFiles.IsFile, path);
                            detailFiles.Permission = permission;
                        }
                        reader.Close();
                        detailFiles.Location = (rootDirectory + GetFilterPath(detailsID) + detailFiles.Name).Replace("/", @"\");
                    }
                    else
                    {
                        detailFiles = new FileDetails();
                        foreach (var item in data)
                        {
                            detailFiles.Name = previousName == "" ? previousName = item.Name : previousName = previousName + ", " + item.Name;
                            previousPath = previousPath == "" ? rootDirectory + item.FilterPath : previousPath;
                            if (previousPath == rootDirectory + item.FilterPath && !isVariousFolders)
                            {
                                previousPath = rootDirectory + item.FilterPath;
                                detailFiles.Location = (rootDirectory + (item.FilterPath).Replace("/", @"\")).Substring(0, (previousPath.Length - 1));
                            }
                            else
                            {
                                isVariousFolders = true;
                                detailFiles.Location = "Various Folders";
                            }
                            if (item.IsFile) size = size + item.Size;
                        }
                        detailFiles.Size = ByteConversion((long)(size + folderValue));
                        detailFiles.MultipleFiles = true;
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                finally { sqlConnection.Close(); }
                getDetailResponse.Details = detailFiles;
                return getDetailResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                getDetailResponse.Error = error;
                return getDetailResponse;
            }
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

        public virtual AccessPermission GetFilePermission(string id, string parentId, string path)
        {
            string fileName = Path.GetFileName(path);
            return GetPermission(id, parentId, fileName, true, path);
        }

        // Returns the image
        public FileStreamResult GetImage(string path, string parentId, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            try
            {
                AccessPermission permission = GetFilePermission(id, parentId, path);
                if (permission != null && !permission.Read)
                {
                    return null;
                }
                sqlConnection = setSQLDBConnection();
                sqlConnection.Open();
                SqlCommand myCommand = new SqlCommand("select * from " + tableName + " where ItemID =" + id, sqlConnection);
                SqlDataReader myReader = myCommand.ExecuteReader();
                while (myReader.Read())
                {
                    try
                    {
                        return new FileStreamResult(new MemoryStream((byte[])myReader["Content"]), "APPLICATION/octet-stream"); ;
                    }
                    catch (Exception ex) { throw ex; }
                }
                sqlConnection.Close();
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        // Deletes the file(s) or folder(s)
        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse remvoeResponse = new FileManagerResponse();
            string ParentID = "";
            try
            {
                FileManagerDirectoryContent deletedData = new FileManagerDirectoryContent();
                List<FileManagerDirectoryContent> newData = new List<FileManagerDirectoryContent>();
                sqlConnection = setSQLDBConnection();
                foreach (var file in data)
                {
                    AccessPermission permission = GetPermission(file.Id, file.ParentID, file.Name, file.IsFile, path);
                    if (permission != null && (!permission.Read || !permission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + file.Name + "' is not accessible.  you need permission to perform the write action.");
                    }
                    try
                    {
                        sqlConnection.Open();
                        SqlDataReader idreader = (new SqlCommand(("select ParentID from " + this.tableName + " where ItemID='" + file.Id + "'"), sqlConnection)).ExecuteReader();
                        while (idreader.Read()) { ParentID = idreader["ParentID"].ToString(); }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                    try
                    {
                        int count;
                        sqlConnection.Open();
                        count = (int)(new SqlCommand("select COUNT(*) from " + this.tableName + " where ParentID='" + ParentID + "' AND MimeType= 'folder' AND Name <> '" + file.Name + "'", sqlConnection)).ExecuteScalar();
                        sqlConnection.Close();
                        if (count == 0)
                        {
                            sqlConnection.Open();
                            SqlCommand updatecommand = new SqlCommand(("update " + this.tableName + " SET HasChild='False' where ItemId='" + ParentID + "'"), sqlConnection);
                            updatecommand.ExecuteNonQuery();
                            sqlConnection.Close();
                        }
                        sqlConnection.Open();
                        SqlDataReader reader = (new SqlCommand("select * from " + this.tableName + " where ItemID='" + file.Id + "'", sqlConnection)).ExecuteReader();
                        while (reader.Read())
                        {
                            deletedData = new FileManagerDirectoryContent
                            {
                                Name = reader["Name"].ToString().Trim(),
                                Size = (long)reader["Size"],
                                IsFile = (bool)reader["IsFile"],
                                DateModified = (DateTime)reader["DateModified"],
                                DateCreated = (DateTime)reader["DateCreated"],
                                Type = GetDefaultExtension(reader["MimeType"].ToString()),
                                HasChild = (bool)reader["HasChild"],
                                Id = reader["ItemID"].ToString()
                            };
                        }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                    try
                    {
                        sqlConnection.Open();
                        SqlCommand deleteCommand = new SqlCommand("delete  from " + this.tableName + " where ItemID='" + file.Id + "'", sqlConnection);
                        deleteCommand.ExecuteNonQuery();
                        string absoluteFilePath = Path.Combine(Path.GetTempPath(), file.Name);
                        var tempDirectory = new DirectoryInfo(Path.GetTempPath());
                        foreach (var newFileName in Directory.GetFiles(tempDirectory.ToString()))
                        {
                            if (newFileName.ToString() == absoluteFilePath)
                            {
                                File.Delete(newFileName);
                            }

                        }
                    }
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }
                    newData.Add(deletedData);
                    remvoeResponse.Files = newData;
                }
                sqlConnection.Open();
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlCommand removeCommand = new SqlCommand(removeQuery, sqlConnection);
                SqlDataReader removeCommandReader = removeCommand.ExecuteReader();
                while (removeCommandReader.Read()) { this.deleteFilesId.Add(removeCommandReader["ItemID"].ToString()); }
                sqlConnection.Close();
                if (this.deleteFilesId.Count > 0)
                {
                    sqlConnection.Open();
                    SqlCommand updateTableCommand = new SqlCommand(("delete from " + this.tableName + " where ItemID IN (" + string.Join(", ", this.deleteFilesId.Select(f => "'" + f + "'")) + ")"), sqlConnection);
                    SqlDataReader getDetailsCommandReader = updateTableCommand.ExecuteReader();
                    sqlConnection.Close();
                }
                this.deleteFilesId = null;
                return remvoeResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                remvoeResponse.Error = error;
                return remvoeResponse;
            }
        }
        // Uploads the files
        public virtual FileManagerResponse Upload(string path, IList<IFormFile> uploadFiles, string action, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse uploadResponse = new FileManagerResponse();

            try
            {
                AccessPermission permission = GetPermission(data[0].Id, data[0].ParentID, data[0].Name, data[0].IsFile, path);
                if (permission != null && (!permission.Read || !permission.Upload))
                {
                     accessMessage = permission.Message;
                     throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the upload action.");
                }
                List<string> existFiles = new List<string>();
                foreach (IFormFile file in uploadFiles)
                {
                    if (uploadFiles != null)
                    {
                        string fileName = Path.GetFileName(file.FileName);
                        string absoluteFilePath = Path.Combine(Path.GetTempPath(), fileName);
                        string contentType = file.ContentType;
                        if (action == "save")
                        {
                            if (!System.IO.File.Exists(absoluteFilePath))
                            {
                                using (FileStream fsSource = new FileStream(absoluteFilePath, FileMode.Create))
                                {
                                    file.CopyTo(fsSource);
                                    fsSource.Close();
                                }
                                using (FileStream fsSource = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read))
                                {
                                    BinaryReader binaryReader = new BinaryReader(fsSource);
                                    long numBytes = new FileInfo(absoluteFilePath).Length;
                                    byte[] bytes = binaryReader.ReadBytes((int)numBytes);
                                    UploadQuery(fileName, contentType, bytes, data[0].Id);
                                }
                            }
                            else
                            {
                                existFiles.Add(fileName);
                            }
                        }
                        else if (action == "replace")
                        {
                            FileManagerResponse detailsResponse = this.GetFiles(path, false, data);                         
                            if (System.IO.File.Exists(absoluteFilePath))
                            {
                                System.IO.File.Delete(absoluteFilePath);

                                foreach (FileManagerDirectoryContent newData in detailsResponse.Files)
                                {
                                    string existingFileName = newData.Name.ToString();
                                    if (existingFileName == fileName)
                                    {
                                        this.Delete(path, null, newData);
                                    }
                                }
                            }
                            using (FileStream fsSource = new FileStream(absoluteFilePath, FileMode.Create))
                            {
                                file.CopyTo(fsSource);
                                fsSource.Close();
                            }
                            using (FileStream fsSource = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read))
                            {
                                BinaryReader binaryReader = new BinaryReader(fsSource);
                                long numBytes = new FileInfo(absoluteFilePath).Length;
                                byte[] bytes = binaryReader.ReadBytes((int)numBytes);
                                UploadQuery(fileName, contentType, bytes, data[0].Id);
                             }
                        }
                        else if (action == "keepboth")
                        {
                            string newAbsoluteFilePath = absoluteFilePath;
                            int index = newAbsoluteFilePath.LastIndexOf(".");
                            if (index >= 0)
                            {
                                newAbsoluteFilePath = newAbsoluteFilePath.Substring(0, index);
                            }
                            int fileCount = 0;
                            while (System.IO.File.Exists(newAbsoluteFilePath + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))))
                            {
                                fileCount++;
                            }

                            newAbsoluteFilePath = newAbsoluteFilePath + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(fileName);
                            string newFileName = Path.GetFileName(newAbsoluteFilePath);
                            using (FileStream fsSource = new FileStream(newAbsoluteFilePath, FileMode.Create))
                            {
                                file.CopyTo(fsSource);
                                fsSource.Close();
                            }
                            using (FileStream fsSource = new FileStream(newAbsoluteFilePath, FileMode.Open, FileAccess.Read))
                            {
                                BinaryReader binaryReader = new BinaryReader(fsSource);
                                long numBytes = new FileInfo(newAbsoluteFilePath).Length;
                                byte[] bytes = binaryReader.ReadBytes((int)numBytes);
                                UploadQuery(newFileName, contentType, bytes, data[0].Id);
                            }
                        }
                    }
                }
                if (existFiles.Count != 0)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.FileExists = existFiles;
                    error.Code = "400";
                    error.Message = "File Already Exists";
                    uploadResponse.Error = error;
                }
                return uploadResponse;
            }
            catch (Exception e) 
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                uploadResponse.Error = error;
                return uploadResponse;
            }
            
        }
        // Updates the data table after uploading the file
        public void UploadQuery(string fileName, string contentType, byte[] bytes, string parentId)
        {
            sqlConnection = setSQLDBConnection();
            sqlConnection.Open();
            SqlCommand command = new SqlCommand("INSERT INTO " + tableName + " (Name, ParentID, Size, IsFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type) VALUES ( @Name, @ParentID, @Size, @IsFile, @MimeType, @Content, @DateModified, @DateCreated, @HasChild, @IsRoot, @Type )", sqlConnection);
            command.Parameters.Add(new SqlParameter("@Name", fileName));
            command.Parameters.Add(new SqlParameter("@IsFile", true));
            command.Parameters.Add(new SqlParameter("@Size", bytes.Length));
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
        // Converts the file to byte array
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
                AccessPermission permission = GetPermission(data[0].Id, data[0].ParentID, data[0].Name, data[0].IsFile, path);
                if (permission != null && (!permission.Read || !permission.Write))
                {
                    accessMessage = permission.Message;
                    throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission");
                }

                FileManagerDirectoryContent renameData = new FileManagerDirectoryContent();
                sqlConnection = setSQLDBConnection();
                try
                {
                    sqlConnection.Open();
                    SqlCommand updatecommand = new SqlCommand(("update " + this.tableName + " set Name='" + newName + "' , DateModified='" + DateTime.Now.ToString() + "' where ItemID ='" + data[0].Id + "'"), sqlConnection);
                    updatecommand.ExecuteNonQuery();
                    sqlConnection.Close();
                    if (newName.Substring(newName.LastIndexOf(".") + 1) != name.Substring(name.LastIndexOf(".") + 1))
                    {
                        sqlConnection.Open();
                        string fileExtension = Path.GetExtension(newName);
                        string mimeType = "application/unknown";
                        Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(fileExtension);
                        if (regKey != null && regKey.GetValue("Content Type") != null)
                            mimeType = regKey.GetValue("Content Type").ToString();
                        string updateTypeQuery = "update " + this.tableName + " set MimeType='" + mimeType + "' where ItemID ='" + data[0].Id + "'";
                        SqlCommand updateTypecommand = new SqlCommand(updateTypeQuery, sqlConnection);
                        updateTypecommand.ExecuteNonQuery();
                        sqlConnection.Close();
                    }
                    try
                    {
                        sqlConnection.Open();
                        SqlDataReader reader = (new SqlCommand(("select * from " + this.tableName + " where ItemID='" + data[0].Id + "'"), sqlConnection)).ExecuteReader();
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
                    catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                    finally { sqlConnection.Close(); }

                }
                catch (SqlException ex) { Console.WriteLine(ex.ToString()); }
                finally { sqlConnection.Close(); }
                var newData = new FileManagerDirectoryContent[] { renameData };
                renameResponse.Files = newData;
                return renameResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                renameResponse.Error = error;
                return renameResponse;
            }
        }

        public string GetFilterPath(string id)
        {
            List<string> parents = new List<string>();
            string idValue = (new SqlCommand(("select ParentID from " + this.tableName + " where ItemID='" + id + "'"), sqlConnection)).ExecuteScalar().ToString().Trim();
            string query = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ItemID =" + idValue + " union all select p.ItemID, p.Name, p.ParentID from " + this.tableName + " p inner join cte on cte.ParentID = p.ItemID) select Name from cte where ParentID != 0";
            SqlCommand queryCommand = new SqlCommand(query, sqlConnection);
            SqlDataReader reader = queryCommand.ExecuteReader();
            while (reader.Read()) { parents.Add(reader["Name"].ToString().Trim()); }
            reader.Close();
            return ("/" + (parents.Count > 0 ? (string.Join("/", parents.ToArray().Reverse()) + "/") : ""));
        }

        public string GetFilterId(string id)
        {
            List<string> parents = new List<string>();
            string idValue = (new SqlCommand(("select ParentID from " + this.tableName + " where ItemID='" + id + "'"), sqlConnection)).ExecuteScalar().ToString().Trim();
            string query = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ItemID =" + idValue + " union all select p.ItemID, p.Name, p.ParentID from " + this.tableName + " p inner join cte on cte.ParentID = p.ItemID) select ItemID from cte";
            SqlCommand queryCommand = new SqlCommand(query, sqlConnection);
            SqlDataReader reader = queryCommand.ExecuteReader();
            while (reader.Read()) { parents.Add(reader["ItemID"].ToString().Trim()); }
            reader.Close();
            return (string.Join("/", parents.ToArray().Reverse()) + "/");
        }
        // Search for file(s) or folder(s)
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {
            sqlConnection = setSQLDBConnection();
            FileManagerResponse searchResponse = new FileManagerResponse();
            try
            {
                if (path == null) { path = string.Empty; };
                var searchWord = searchString;
                bool hasPermission = true;
                FileManagerDirectoryContent searchData;
                FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
                cwd.Name = data[0].Name;
                cwd.Size = data[0].Size;
                cwd.IsFile = false;
                cwd.DateModified = data[0].DateModified;
                cwd.DateCreated = data[0].DateCreated;
                cwd.HasChild = data[0].HasChild;
                cwd.Type = data[0].Type;
                sqlConnection.Open();
                cwd.FilterPath = GetFilterPath(data[0].Id);
                sqlConnection.Close();
                AccessPermission permission = GetPermission(data[0].Id, data[0].ParentID, cwd.Name, cwd.IsFile, path);
                cwd.Permission = permission;
                if (cwd.Permission != null && !cwd.Permission.Read)
                {
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
                searchResponse.CWD = cwd;
                List<FileManagerDirectoryContent> foundFiles = new List<FileManagerDirectoryContent>();
                List<string> availableFiles = new List<string>();
                sqlConnection.Open();
                string removeQuery = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlCommand childCommand = new SqlCommand(removeQuery, sqlConnection);
                SqlDataReader childCommandReader = childCommand.ExecuteReader();
                while (childCommandReader.Read()) { availableFiles.Add(childCommandReader["ItemID"].ToString()); }
                sqlConnection.Close();
                if (availableFiles.Count > 0)
                {
                    sqlConnection.Open();
                    SqlDataReader reader = (new SqlCommand("select * from " + this.tableName + " where Name like '" + searchString.Replace("*", "%") + "' AND ItemID IN(" + string.Join(", ", availableFiles.Select(f => "'" + f + "'")) + ")", sqlConnection)).ExecuteReader();
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
                            Id = reader["ItemId"].ToString().Trim(),
                            ParentID = reader["ParentID"].ToString().Trim(),
                        };
                        AccessPermission searchPermission = GetPermission(searchData.Id, searchData.ParentID, searchData.Name, searchData.IsFile, path);
                        searchData.Permission = searchPermission;
                        if (searchData.Name != "Products") foundFiles.Add(searchData);
                    }
                    reader.Close();

                    for (int i = foundFiles.Count - 1; i >= 0; i--)
                    {
                        foundFiles[i].FilterPath = GetFilterPath(foundFiles[i].Id);
                        foundFiles[i].FilterId = GetFilterId(foundFiles[i].Id);
                        hasPermission = parentsHavePermission(foundFiles[i]);
                        if (!hasPermission)
                        {
                            foundFiles.Remove(foundFiles[i]);
                        }
                    }
                }
                searchResponse.Files = (IEnumerable<FileManagerDirectoryContent>)foundFiles;
                return searchResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                searchResponse.Error = error;
                return searchResponse;
            }
            finally { sqlConnection.Close(); }
        }
        protected virtual bool parentsHavePermission(FileManagerDirectoryContent fileDetails)
        {
            String[] parentPath = fileDetails.FilterId.Split('/');
            bool hasPermission = true;
            for (int i = 0; i <= parentPath.Length - 3; i++)
            {
                AccessPermission pathPermission = GetPermission(fileDetails.ParentID, parentPath[i], fileDetails.Name, false, fileDetails.FilterId);
                if (pathPermission == null)
                {
                    break;
                }
                else if (pathPermission != null && !pathPermission.Read)
                {
                    hasPermission = false;
                    break;
                }
            }
            return hasPermission;
        }
        // Copies the selected folder
        public void CopyFolderFiles(string[] fileId, string[] newTargetId, SqlConnection sqlConnection)
        {
            List<string> fromFoldersId = new List<string>();
            List<string> toFoldersId = new List<string>();
            for (var i = 0; i < fileId.Length; i++)
            {
                string copyQuery = "insert into " + tableName + " (Name, ParentID, Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath) select Name," + newTargetId[i] + " , Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath from " + tableName + " where ParentID = " + fileId[i];
                SqlCommand copyQuerycommand = new SqlCommand(copyQuery, sqlConnection);
                copyQuerycommand.ExecuteNonQuery();
                SqlDataReader checkingQuerycommandReader = (new SqlCommand(("Select * from " + tableName + " where ParentID =" + newTargetId[i] + " and MimeType = 'folder'"), sqlConnection)).ExecuteReader();
                while (checkingQuerycommandReader.Read()) { toFoldersId.Add(checkingQuerycommandReader["ItemID"].ToString().Trim()); }
                checkingQuerycommandReader.Close();
                SqlDataReader tocheckingQuerycommandReader = (new SqlCommand(("Select * from " + tableName + " where ParentID =" + fileId[i] + " and MimeType = 'folder'"), sqlConnection)).ExecuteReader();
                while (tocheckingQuerycommandReader.Read()) { fromFoldersId.Add(tocheckingQuerycommandReader["ItemID"].ToString().Trim()); }
                tocheckingQuerycommandReader.Close();
            }
            if (fromFoldersId.Count > 0)
                CopyFolderFiles(fromFoldersId.ToArray(), toFoldersId.ToArray(), sqlConnection);
        }
        // Gets the last inserted value
        public string GetLastInsertedValue()
        {
            string getIdQuery = "SELECT SCOPE_IDENTITY()";
            SqlCommand copyQuerycommand = new SqlCommand(getIdQuery, sqlConnection);
            return copyQuerycommand.ExecuteScalar().ToString().Trim();
        }
        // Copies the selected file(s) or folder(s)
        public FileManagerResponse Copy(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            List<string> checkingId = new List<string>();
            sqlConnection = setSQLDBConnection();
            FileManagerResponse copyResponse = new FileManagerResponse();
            try
            {
                sqlConnection.Open();
                string checkingQuery = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlDataReader copyCheckCommandReader = (new SqlCommand(checkingQuery, sqlConnection)).ExecuteReader();
                while (copyCheckCommandReader.Read()) { checkingId.Add(copyCheckCommandReader["ItemID"].ToString()); }
                sqlConnection.Close();

                AccessPermission permission = GetPermission(data[0].Id, data[0].ParentID, data[0].Name, data[0].IsFile, path);
                if (permission != null && (!permission.Read || !permission.WriteContents))
                {
                    accessMessage = permission.Message;
                    throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the copy action.");
                }
                if (checkingId.IndexOf(targetData.Id) != -1)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.Code = "400";
                    error.Message = "The destination folder is the subfolder of the source folder.";
                    copyResponse.Error = error;
                    return copyResponse;
                }
                foreach (var item in data)
                {
                    try
                    {
                        sqlConnection.Open();
                        List<string> foldersId = new List<String>();
                        List<string> lastInsertedItemId = new List<String>();
                        string lastId = "";
                        string copyQuery = "insert into " + tableName + " (Name, ParentID, Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath) select Name," + targetData.Id + " , Size, isFile, MimeType, Content, DateModified, DateCreated, HasChild, IsRoot, Type, FilterPath from " + tableName + " where ItemID = " + item.Id;
                        SqlCommand copyQuerycommand = new SqlCommand(copyQuery, sqlConnection);
                        copyQuerycommand.ExecuteNonQuery();
                        lastId = GetLastInsertedValue();
                        sqlConnection.Close();
                        sqlConnection.Open();
                        SqlDataReader reader = (new SqlCommand(("Select * from " + this.tableName + " where ItemID=" + item.Id), sqlConnection)).ExecuteReader();
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
                                FilterPath = targetData.FilterPath + "/" + targetData.Name + "/",
                                Type = GetDefaultExtension(reader["MimeType"].ToString()),
                                Id = reader["ItemID"].ToString()
                            };
                            if (!copyFiles.IsFile)
                            {
                                foldersId.Add(copyFiles.Id);
                                lastInsertedItemId.Add(lastId);
                            }
                            files.Add(copyFiles);
                        }
                        reader.Close();
                        foreach (var file in files) { file.FilterId = GetFilterId(file.Id); }
                        if (foldersId.Count > 0)
                            CopyFolderFiles(foldersId.ToArray(), lastInsertedItemId.ToArray(), sqlConnection);
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                    finally { sqlConnection.Close(); }
                }
                copyResponse.Files = files;
                return copyResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                copyResponse.Error = error;
                return copyResponse;
            }
        }

        public virtual string[] GetFolderDetails(string path)
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

        // Moves the selected file(s) or folder(s) to target path
        public FileManagerResponse Move(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            sqlConnection = setSQLDBConnection();
            FileManagerResponse moveResponse = new FileManagerResponse();
            try
            {
                List<string> checkingId = new List<string>();
                sqlConnection.Open();
                string checkingQuery = "with cte as (select ItemID, Name, ParentID from " + this.tableName + " where ParentID =" + data[0].Id + " union all select p.ItemID, p.Name, p.ParentID from Product p inner join cte on p.ParentID = cte.ItemID) select ItemID from cte;";
                SqlDataReader moveCheckCommandReader = (new SqlCommand(checkingQuery, sqlConnection)).ExecuteReader();
                while (moveCheckCommandReader.Read()) { checkingId.Add(moveCheckCommandReader["ItemID"].ToString()); }
                sqlConnection.Close();

                AccessPermission permission = GetPermission(data[0].Id, data[0].ParentID, data[0].Name, data[0].IsFile, path);
                if (permission != null && (!permission.Read || !permission.WriteContents))
                {
                    accessMessage = permission.Message;
                    throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the write action.");
                }

                if (checkingId.IndexOf(targetData.Id) != -1)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.Code = "400";
                    error.Message = "The destination folder is the subfolder of the source folder.";
                    moveResponse.Error = error;
                    return moveResponse;
                }
                foreach (var item in data)
                {
                    sqlConnection.Open();
                    try
                    {
                        string moveQuery = "update " + this.tableName + " SET ParentID='" + targetData.Id + "' where ItemID='" + item.Id + "'";
                        SqlCommand moveQuerycommand = new SqlCommand(moveQuery, sqlConnection);
                        moveQuerycommand.ExecuteNonQuery();
                        sqlConnection.Close();
                        sqlConnection.Open();
                        string detailsQuery = "Select * from " + this.tableName + " where ItemID=" + item.Id;
                        SqlCommand detailQuerycommand = new SqlCommand(detailsQuery, sqlConnection);
                        SqlDataReader reader = detailQuerycommand.ExecuteReader();
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
                                FilterPath = targetData.FilterPath + "/" + targetData.Name + "/",
                                Type = GetDefaultExtension(reader["MimeType"].ToString()),
                                Id = reader["ItemID"].ToString()
                            };
                            files.Add(moveFiles);
                        }
                        reader.Close();
                        foreach (var file in files) { file.FilterId = GetFilterId(file.Id); }
                    }
                    catch (Exception e) { throw e; }
                    finally { sqlConnection.Close(); }
                }
                moveResponse.Files = files;
                return moveResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Message = e.Message.ToString();
                error.Code = e.Message.ToString().Contains("is not accessible. You need permission") ? "401" : "417";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                moveResponse.Error = error;
                return moveResponse;
            }
        }

        // Converts the byte value to the appropriate size value
        public String ByteConversion(long fileSize)
        {
            try
            {
                string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
                if (fileSize == 0) return "0 " + index[0];
                int loc = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(fileSize), 1024)));
                return (Math.Sign(fileSize) * Math.Round(Math.Abs(fileSize) / Math.Pow(1024, loc), 1)).ToString() + " " + index[loc];
            }
            catch (Exception e) { throw e; }
        }

        public string ToCamelCase(FileManagerResponse userData)
        {
            return JsonConvert.SerializeObject(userData, new JsonSerializerSettings { ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() } });
        }
    }
}

