using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;
using Siganberg.SqlGen;

GenerateSqlFiles();

void GenerateSqlFiles()
{

    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
        
    Log.Logger.Information("Generating SQL Generator starting..");
    
    Log.Logger.Information("Generating SQL Generator starting...");
    var currentDirectory = Directory.GetCurrentDirectory();
    var jsonConfig = currentDirectory + "/" + "sqlgen.json";
    

    if (!File.Exists(jsonConfig))
    {
        Log.Logger.Information("sqlgen.json cannot be found");
        return;
    }

    var configFile = File.ReadAllText(jsonConfig);
    var configModel = JsonSerializer.Deserialize<SqlGenConfig>(configFile);

    if (configModel == null)
    {
        Log.Logger.Information("sqlgen.json has invalid content");
        return;
    }

    var targetPath = currentDirectory + "/" + configModel.TargetPath;
    var connection = new ServerConnection(configModel.Server, configModel.Username, configModel.Password);
    var server = new Server(connection);
    var visited = new HashSet<string>();
    foreach (var d in configModel.Databases)
    {
        var schemas = new HashSet<string>();
        Log.Logger.Information("Generating database scripts for {Name}", d.Name);
        var database = server.Databases[d.Name];
        if (database == null)
        {
            Log.Logger.Information("Database {Name} does not exist", d.Name);
            continue;
        }
        
        CreateDatabaseFolder(targetPath, d);
        
        Log.Logger.Information("Table scripts...");
        foreach(var table in d.Tables)
            GenerateTableContent(table, database, visited, $"{targetPath}/{d.GeneratedName}", schemas);

        Log.Logger.Information("StoredProcedure scripts...");
        foreach (var sp in d.StoredProcedures)
            GenerateStoredProcedures(sp, database, $"{targetPath}/{d.GeneratedName}", schemas);
        
        Log.Logger.Information("Schema scripts...");
        foreach (var schema in schemas)
            GenerateSchema(schema, $"{targetPath}/{d.GeneratedName}");
    }
    Log.Logger.Information("Generation completed!");

}

void GenerateSchema(string schemaName, string targetPath)
{
    var path = $"{targetPath}/Schemas/{schemaName}.sql";
    using var file = File.CreateText(path);
    Log.Logger.Information("Generating file: {Path}", path);
        file.WriteLine(schemaName);
    file.Flush();
    file.Close();
}

void GenerateStoredProcedures(string storeProcedureName, Database database, string targetPath, HashSet<string> schemas)
{
    var split = storeProcedureName.Split(".");
    var storedProcedure = database.StoredProcedures[split[1].StripBracket(), split[0].StripBracket()];
    
    if (storedProcedure == null)
    {
        Log.Logger.Information("StoredProcedure {StoredProcedureName} does not exist", storeProcedureName);
        return;
    }

    schemas.Add(storedProcedure.Schema);
    
    var tableScripts = storedProcedure.Script();
    var path = $"{targetPath}/StoredProcedures/{storedProcedure.Schema}.{storedProcedure.Name}.sql";
    using var file = File.CreateText(path);
    Log.Logger.Information("Generating file: {Path}", path);
    foreach (var script in tableScripts)
    {
        file.WriteLine(script);
    }
    file.Flush();
    file.Close();
}

void CreateDatabaseFolder(string targetPath, SqlGenConfig.Database database)
{
    var databaseName = string.IsNullOrEmpty(database.FolderName) ? database.Name : database.FolderName;
    var databasePath = targetPath + $"/{databaseName}";
    
    CreateDirectoryIfNotExist(databasePath + "/Tables");
    CreateDirectoryIfNotExist(databasePath + "/StoredProcedures");
    CreateDirectoryIfNotExist(databasePath + "/Schemas");
}

void CreateDirectoryIfNotExist(string path)
{
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path!);
}

void GenerateTableContent(string tableName, Database database, HashSet<string> visited, string targetPath, HashSet<string> schemas, bool isReference = false)
{
    if (visited.Contains(tableName)) return;
    visited.Add(tableName);

    var split = tableName.Split(".");
        
    var table = database.Tables[split[1].StripBracket(), split[0].StripBracket()];

    if (table == null)
    {
        Log.Logger.Information("Table {TableName} does not exist", tableName);
        return;
    }
    
    if (isReference)
        Log.Logger.Information("Reference table found. {TableName}. Including in the generation if not part of the list", tableName);

    schemas.Add(table.Schema);
        
    for (var i = 0; i < table.ForeignKeys.Count; i++)
    {
        var reference = table.ForeignKeys[i];
        var parentTableName = $"[{reference.ReferencedTableSchema}].[{reference.ReferencedTable}]";
        GenerateTableContent(parentTableName, database, visited, targetPath, schemas, true);
    }
        
    var scriptOptions = new ScriptingOptions
    {
        ScriptForCreateDrop = true,
        Indexes = true,
        IncludeIfNotExists = false,
        DriForeignKeys = true,
        DriAllConstraints = true,
    };
        
    var tableScripts = table.Script(scriptOptions);
    var path = $"{targetPath}/Tables/{table.Schema}.{table.Name}.sql";
    using var file = File.CreateText(path);
    Log.Logger.Information("Generating file: {Path}", path);
    foreach (var script in tableScripts)
    {
        file.WriteLine(script);
    }
    file.Flush();
    file.Close();
}