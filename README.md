# Siganberg.SqlGen [![Nuget](https://img.shields.io/nuget/v/Siganberg.SqlGen)](https://www.nuget.org/packages/Siganberg.SqlGen/) [![Nuget](https://img.shields.io/nuget/dt/Siganberg.SqlGen)](https://www.nuget.org/packages/Siganberg.SqlGen/)


## About

This dotnet tools can generate `CREATE` sql scripts for stored procedures, tables and schemas. You just need to create `sqlgen.json` that contains connection information such as server and credentials, list of databases, stored procedures and tables. The tool can automatically detect `REFERENCES` for `TABLE` and will automatically include it in the generation.  

I use this tool to generate scripts from an existing database and use EF migration for provisioning the development database. The generated scripts can also be used in our CI pipeline to initialize an empty database before running all integration tests. 

## Installtion 

To install the Siganberg.SqlGen NuGet package into your app.

```console
dotnet tool install --global Siganberg.SqlGen
```



## Sample `sqlgen.json`

```json
{
  "Server" : "{YOUR-SQL-SERVER}",
  "Username" : "{username}",
  "Password" : "{password}",
  "TargetPath" : "Migrations",
  "Databases" : [
    {
      "Name":"Shop",
      "FolderName" : "ShopMigration",
      "Tables" : [
        "[shop].[TBL_Orders]",
        "[shop].[TBL_OrderLineItems]",
      ],
      "StoredProcedures" : [
        "[shop].[spx_Get_Orders]",
        "[shop].[spx_Get_Orders_With_Items]"
      ]
    },
    {
      "Name":"Inventory",
      "FolderName" : "InventoryMigration",
      "Tables" : [
        "[inventory].[TBL_Products]"
      ]
    }
  ]
}
```


## Usage

On CLI (command-line interface), execute the following command to start the generation. 

```bash
/<path_where_sqlgen.json>/sqlgen
```



## Setting definition


| Property | Default | Descriptions                                                                                                                                       |
|---------------------|---------|----------------------------------------------------------------------------------------------------------------------------------------------------|
|     SERVER                 | no default (required)   | Database server name. |                   |
|     Username                | no default (required)   | Database username.  |
|     Password                | no default (required)   | Database password. |
|     TargetPath                | empty   | The base path of the generated scripts will be the location of `sqlgen.json` plus the ***TargetPath***. BasePath = `/{sqlgen.json path}/{TargetPath}`.  |
|     Databases                | no default (required)   | List of databases that contain SQL object to generate. |
|     FolderName                | empty   | If ***FolderName*** is empty it will use the ***Name*** as the FolderName. Output format will be `/{BasePath}/{FolderName}`.  | 
|     Tables                | empty   | Array/List of table names. Format should be `[schema].[tableName]`  | 
|     Stored Procedures                | empty   | Array/List of stored procedures. Format should be `[schema].[storedProcedureName]` | 