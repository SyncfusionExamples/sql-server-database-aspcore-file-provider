# SQL server database file provider for Essential JS2 File Manager

This repository contains the SQL server database file provider in ASP.NET Core for the Syncfusion File Manager component.

To know more about SQL server database file system provider for File Manager, please refer our documentation [here]
(https://ej2.syncfusion.com/aspnetcore/documentation/file-manager/file-system-provider#sql-database-file-system-provider).

## Key Features

The SQL FILESTREAM feature provides efficient storage, management, and streaming of unstructured data stored as files on the file system.

The SQL server database file provider serves the file system support for the FileManager component in SQL server database.

The following actions can be performed with the SQL server database file provider.

| **Actions** | **Description** |
| --- | --- |
| Read      | Read the files from SQL table. |
| Details   | Gets a file's details which consists of Type, Size, Location and Modified date. |
| Download  | Downloads the selected file or folder from the SQL table. |
| Upload    | Uploads a file to the SQL table. It accepts uploaded media with the following characteristics: <ul><li>Maximum file size:  30MB</li><li>Accepted Media MIME types: `*/*` </li></ul> |
| Create    | Creates a New folder. |
| Delete    | Deletes a folder or file. |
| Copy      | Copys the selected Files from target. |
| Move      | Pastes the copied files to the desired location. |
| Rename    | Renames a folder or file. |
| Search    | Full-text queries perform linguistic searches against text data in full-text indexes by operating on words and phrases. |

## Prerequisites

* Visual Studio 2022

Make the SQL server connection with SQL database file ([App_Data/FileManager.mdf](https://github.com/SyncfusionExamples/sql-server-database-aspcore-file-provider/tree/master/App_Data)) and specify the connection string in "Web.config" file as specified in below code example.

```

<add name="FileExplorerConnection" connectionString="Data Source=(LocalDB)\v11.0;AttachDbFilename=|DataDirectory|\FileManager.mdf;Integrated Security=True;Trusted_Connection=true" />

```

Also need to add the entry for the connection string in the [`appsettings.json`](https://github.com/SyncfusionExamples/sql-server-database-aspcore-file-provider/blob/master/appsettings.json) file as specified in below code example.

```

{
  "ConnectionStrings": {
    "FileManagerConnection": "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\\App_Data\\FileManager.mdf;Integrated Security=True;Connect Timeout=30"
  }
}

```

To configure the SQL server database connection use the `SetSQLConnection` method to set the connection name, table name and rootId of the table.

```
  
  SetSQLConnection(string name, string tableName, string tableID)

```

## How to run the project

* Checkout this project to a location in your disk.
* Open the solution file using Visual Studio 2022.
* Restore the NuGet packages by rebuilding the solution.
* Run the project.

## File Manager AjaxSettings

To access the basic actions such as Read, Delete, Copy, Move, Rename, Search, and Get Details of File Manager using the SQL server database file provider service, just map the following code snippet in the Ajaxsettings property of File Manager.

Here, the `hostUrl` will be your locally hosted port number.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/SQLProvider/SQLFileOperations'
  }
```

## File download AjaxSettings

To perform download operation, initialize the `downloadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/SQLProvider/SQLFileOperations',
        downloadUrl: hostUrl + 'api/SQLProvider/SQLDownload'
  }
```

## File upload AjaxSettings

To perform upload operation, initialize the `uploadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/SQLProvider/SQLFileOperations',
        uploadUrl: hostUrl + 'api/SQLProvider/SQLUpload'
  }
```

## File image preview AjaxSettings

To perform image preview support in the File Manager component, initialize the `getImageUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/SQLProvider/SQLFileOperations',
         getImageUrl: hostUrl + 'api/SQLProvider/SQLGetImage'
  }
```

The FileManager will be rendered as the following.

![File Manager](https://ej2.syncfusion.com/products/images/file-manager/readme.gif)


## Support

Product support is available for through following mediums.

* Creating incident in Syncfusion [Direct-trac](https://www.syncfusion.com/support/directtrac/incidents?utm_source=npm&utm_campaign=filemanager) support system or [Community forum](https://www.syncfusion.com/forums/essential-js2?utm_source=npm&utm_campaign=filemanager).
* New [GitHub issue](https://github.com/syncfusion/ej2-javascript-ui-controls/issues/new).
* Ask your query in [Stack Overflow](https://stackoverflow.com/?utm_source=npm&utm_campaign=filemanager) with tag `syncfusion` and `ej2`.

## License

Check the license detail [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/license).

## Changelog

Check the changelog [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/controls/filemanager/CHANGELOG.md)

Copyright 2023 Syncfusion, Inc. All Rights Reserved. The Syncfusion Essential Studio license and copyright applies to this distribution.