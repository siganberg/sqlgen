# Siganberg.SqlGen [![Nuget](https://img.shields.io/nuget/v/Siganberg.SqlGen)](https://www.nuget.org/packages/Siganberg.SqlGen/) [![Nuget](https://img.shields.io/nuget/dt/Siganberg.SqlGen)](https://www.nuget.org/packages/Siganberg.SqlGen/)


## About

This dotnet tools can generate `CREATE` sql scripts for stored procedures, tables, views and schemas. You just need to create `sqlgen.json` that contains connection information such as server and credentials, list of databases, stored procedures and tables. The tool can automatically detect dependencies and will automatically include in the generation. 

***Why do I even need this tool, I can just use the SQL Studio to generate the whole database?***

Yes you can, but if you are working on legacy system that are not properly segragated, the database can contains thousands of sql objects such tables, stored procedures and views. And if your application only needed few of these sql objets, it is cumbersome to generate them individually in MS SQL Studio manually. And you can also say it's better to have everything, it doesn't harm. Not always the case. For example, I worked on a project that we automate everything in development box and  CI which includes running unit and integration testing. When running integration tests, we start our own instance of SQL Server, run EF migrations using the generated scripts from this tool, run tests, and then tear it down. Everything is done on docker container and executed multiple times in our development machine or on our CI pipeline. So having large migration scripts will drastically slow down this process.


## Installation 

Install the tools globally. 

```console
dotnet tool install --global Siganberg.SqlGen
```

## Usage

The tool can generate sql script  from `sqlgen.json` or from command line parameters. `sqlgen.json` still require to store server and credential information.

This command will generate all sql objects and it's dependencies that are specified in the  `sqlgen.json`.
```console
/<path_where_sqlgen.json>/sqlgen
```


This command will only generate `[ShopDb].[shop].[TBL_Orders]` table and it's dependencies then automatically add it to the `sqlgen.json` once it's done generating.
```console
/<path_where_sqlgen.json>/sqlgen -t "[ShopDb].[shop].[TBL_Orders]"
```

## CLI Parameters

| Property                       | Descriptions                                                              |
|--------------------------------|---------------------------------------------------------------------------|
| -t ***<db.schema.tableName>*** | Generate script for table. You can pass multiple `-t` values.             |   
| -s ***<db.schema.tableName>*** | Generate script for stored procedures. You can pass multiple `-s` values. |   
| -t ***<db.schema.tableName>*** | Generate script for view. You can pass multiple `-s` values.              |   


## Sample `sqlgen.json`

```json
{
  "Server" : "{YOUR-SQL-SERVER}",
  "Username" : "{username}",
  "Password" : "{password}",
  "TargetPath" : "Migrations",
  "Databases" : [
    {
      "Name":"ShopDb",
      "FolderName" : "ShopMigration",
      "Tables" : [
        "[shop].[TBL_Orders]",
        "[shop].[TBL_OrderLineItems]"
      ],
      "StoredProcedures" : [
        "[shop].[spx_Get_Orders]",
        "[shop].[spx_Get_Orders_With_Items]"
      ],
      "Views" : [
        "[shop].[vw_OrderSummary]"
      ]
    },
    {
      "Name":"InventoryDb",
      "FolderName" : "InventoryMigration",
      "Tables" : [
        "[inventory].[TBL_Products]"
      ]
    }
  ]
}
```





## Setting definition


| Property | Default | Descriptions                                                                                                                                       |
|---------------------|---------|----------------------------------------------------------------------------------------------------------------------------------------------------|
|     SERVER                 | no default (required)   | Database server name. |                   |
|     Username                | no default (required)   | Database username.  |
|     Password                | no default (required)   | Database password. |
|     TargetPath                | empty   | The base path of the generated scripts will be the location of `sqlgen.json` plus the ***TargetPath***. BasePath = `/{sqlgen.json path}/{TargetPath}`.  |
|     Databases                | no default (required)   | List of databases that contain SQL object to generate. |
|     Name                | no default (required)   | Name of the database.  |
|     FolderName                | empty   | If ***FolderName*** is empty it will use the ***Name*** as the FolderName. Output format will be `/{BasePath}/{FolderName}`.  | 
|     Tables                | empty   | Array/List of table names. Format should be `[schema].[tableName]`  | 
|     Stored Procedures                | empty   | Array/List of stored procedures. Format should be `[schema].[storedProcedureName]` | 
|     Stored Procedures                | empty   | Array/List of stored procedures. Format should be `[schema].[viewname]` | 