# SQL database file system provider for Essential JS2 File Manager

This repository contains the SQL database file system provider in ASP.NET Core for the Essential JS 2 File Manager component..

## Key Features

The SQL FILESTREAM feature provides efficient storage, management, and streaming of unstructured data stored as files on the file system.

SQL file system provider serves the file system support for the  FileManager component in SQL server database.

The following actions can be performed with SQL file system provider.

- Read      - Read the files from SQL table.
- Details   - Gets a file's details which consists of Type, Size, Location and Modified date.
- Download  - Download the selected file or folder from the SQL table.
- Upload    - Uploads a file to the SQL table. It accepts uploaded media with the following characteristics:
                - Maximum file size:  30MB
                - Accepted Media MIME types: */*
- Create    - Create a New folder.
- Delete    - Delete a folder or file.
- Copy      - Copy the selected Files from target.
- Move      - Paste the copied files to the desired location.
- Rename    - Rename a folder or file.
- Search    - Full-text queries perform linguistic searches against text data in full-text indexes by operating on words and phrases.

## Prerequisites

Make the SQL server connection with SQL database file (FileManager.mdf) and specify the connection string in "Web.config" file as specified in below code example.

```

<add name="FileExplorerConnection" connectionString="Data Source=(LocalDB)\v11.0;AttachDbFilename=|DataDirectory|\FileManager.mdf;Integrated Security=True;Trusted_Connection=true" />

```

To configure the SQL server database connection use the `SetSQLConnection` method to set the connection name, table name and rootId of the table.

```
  
  SetSQLConnection(string name, string tableName, string tableID)

```

## How to run this application?

To run this application, clone the [`ej2-sql-server-database-aspcore-file-provider `](https://github.com/SyncfusionExamples/ej2-sql-server-database-aspcore-file-provider ) repository and then navigate to its appropriate path where it has been located in your system.

To do so, open the command prompt and run the below commands one after the other.

```

git clone https://github.com/SyncfusionExamples/ej2-sql-server-database-aspcore-file-provider   FileManagerSQLService
cd FileManagerSQLService

```

## Running application

Once cloned, open solution file in visual studio.Then build the project and run it after restoring the nuget packages.

## Support

Product support is available for through following mediums.

* Creating incident in Syncfusion [Direct-trac](https://www.syncfusion.com/support/directtrac/incidents?utm_source=npm&utm_campaign=filemanager) support system or [Community forum](https://www.syncfusion.com/forums/essential-js2?utm_source=npm&utm_campaign=filemanager).
* New [GitHub issue](https://github.com/syncfusion/ej2-javascript-ui-controls/issues/new).
* Ask your query in [Stack Overflow](https://stackoverflow.com/?utm_source=npm&utm_campaign=filemanager) with tag `syncfusion` and `ej2`.

## License

Check the license detail [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/license).

## Changelog

Check the changelog [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/controls/filemanager/CHANGELOG.md)

� Copyright 2019 Syncfusion, Inc. All Rights Reserved. The Syncfusion Essential Studio license and copyright applies to this distribution.