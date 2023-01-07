using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using Serilog;

namespace Siganberg.SqlGen;

public class GeneratorService
{
    private readonly ILogger _logger;
    private readonly SqlGenConfig _config;
    private readonly Server _server;
    private readonly string _rootPath;

    public GeneratorService(SqlGenConfig configuration, Server server)
    {
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        _config = configuration;
        _server = server;
        _rootPath = Directory.GetCurrentDirectory() + "/" + _config.TargetPath;
    }

    public void Run(CommandList commandList)
    {
        _logger.Information("SQL Script Generator starting...");

        var updateJson = commandList != null; 
        
        commandList ??= GenerateCommandListFromJson();
      
        GenerateFromCommandList(commandList, updateJson);

        _logger.Information("Generation completed!");
    }

    private void GenerateFromCommandList(CommandList commandList, bool updateJson = false)
    {
        var urns = new HashSet<Urn>();

        foreach (var command in commandList.Commands)
        {
            var dbName = command.DbName;
            if (string.IsNullOrEmpty(dbName))
            {
                dbName = GetFirstDbNameFromJson();
                command.DbName = dbName;
            }
            if (string.IsNullOrEmpty(dbName))
                _logger.Error("dbName: \"{DbName}\" not found", dbName);
            
            var database = GetDatabase(dbName);
            if (database == null) continue;

            var obj = database.Tables[command.Name, command.Schema]
                       ?? (SqlSmoObject)database.StoredProcedures[command.Name, command.Schema]
                       ?? database.Views[command.Name, command.Schema];
            
            
            if (obj == null)
            {
                _logger.Information("[{Schema}].[{Name}] not found in database: {Database}", command.Schema, command.Name, dbName);
                continue;
            }

            command.Type = obj.Urn.Type.Substring(0, 1).ToLower();
            
            urns.Add(obj.Urn);
        }

        if (urns.Count <= 0) return;
        
        DetectDependenciesAndGenerate(urns);
        if (updateJson)
            SaveJsonConfig(commandList);

    }

    private string GetFirstDbNameFromJson()
    {
        return _config.Databases.FirstOrDefault()?.Name;
    }

    private void SaveJsonConfig(CommandList commandList)
    {
        foreach (var command in commandList.Commands)
        {
            var dbConfig = _config.Databases.FirstOrDefault(a => a.Name.ToLower() == command.DbName.ToLower());
            if (dbConfig == null)
            {
                dbConfig = new SqlGenConfig.Database
                {
                    Name = command.DbName
                };
                _config.Databases.Add(dbConfig);
            }

            var fullName = $"[{command.Schema}].[{command.Name}]";
            switch (command.Type)
            {
                case "s":
                    dbConfig.StoredProcedures.Add(fullName);
                    break;
                case "t":
                    dbConfig.Tables.Add(fullName);
                    break;
                case "v":
                    dbConfig.Views.Add(fullName);
                    break;
            }
        }
        
        var path = Directory.GetCurrentDirectory() + "/sqlgen.json";
        _logger.Information("Updating sqlgen.json");
        using var file = File.CreateText(path);
        file.Write(JsonSerializer.Serialize(_config,
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault }));
        file.Close();
      
    }


    private CommandList GenerateCommandListFromJson()
    {
        _logger.Information("Generating from sqlgen.json");
        var commandList = new CommandList();
        foreach (var d in _config.Databases)
        {
            commandList.Commands.AddRange(d.Tables.Select(a => CreateCommand("t", d.Name, a)));
            commandList.Commands.AddRange(d.StoredProcedures.Select(a => CreateCommand("s", d.Name, a)));
            commandList.Commands.AddRange(d.Views.Select(a => CreateCommand("v", d.Name, a)));
        }
        return commandList;
    }

    private Command CreateCommand(string command, string dbName, string schemaAndName)
    {
        var split = schemaAndName.StripBracket().Split(".");
        return new Command
        {
            Type = command,
            DbName = dbName,
            Schema = split[0],
            Name = split[1]
        };
    }

    private void DetectDependenciesAndGenerate(HashSet<Urn> urns)
    {
        var depCollection = CreateDependencyCollection(urns);
        var schemas = GenerateSqlFiles(depCollection);
        foreach (var schema in schemas)
            GenerateSchema(schema);
    }

    private Database GetDatabase(string name)
    {
        var database = _server.Databases[name];
        if (database == null)
            _logger.Information("Database with name {Name} not found", name);
        return database;
    }

    private Database GetDatabase(Urn urn)
    {
        while (urn != null)
        {
            if (urn.Type == "Database")
            {
                var name = urn.GetAttribute("Name");
                var database = _server.Databases[name];
                if (database == null)
                    _logger.Warning("Database with name {Name} doesn't exist", name);
                return database;
            }

            urn = urn.Parent;
        }

        return null;
    }

    private HashSet<string> GenerateSqlFiles(DependencyCollection depCollection)
    {
        var schemas = new HashSet<string>();

        foreach (var dep in depCollection)
        {
            if (dep.Urn.Type == "UnresolvedEntity") continue;
            var database = GetDatabase(dep.Urn);
            if (database == null) continue;
            GenerateSqlFileFromUrn(dep.Urn, database);
            schemas.Add($"{database.Name}.{dep.Urn.GetAttribute("Schema")}");
        }

        return schemas;
    }

    private void GenerateSqlFileFromUrn(Urn urn, Database database)
    {
        switch (urn.Type)
        {
            case "Table":
            {
                var obj = GetSqlObject(urn, (name, schema) => database.Tables[name, schema]);
                GenerateFile(obj, $"{_rootPath}{MapDatabaseToFolderName(database.Name)}/Tables", o => obj.Script(o));
                break;
            }
            case "StoredProcedure":
            {
                var obj = GetSqlObject(urn, (name, schema) => database.StoredProcedures[name, schema]);
                GenerateFile(obj, $"{_rootPath}{MapDatabaseToFolderName(database.Name)}/StoredProcedures", o => obj.Script(o));
                break;
            }
            case "View":
            {
                var obj = GetSqlObject(urn, (name, schema) => database.Views[name, schema]);
                GenerateFile(obj, $"{_rootPath}{MapDatabaseToFolderName(database.Name)}/Views", o => obj.Script(o));
                break;
            }
        }
    }

    private string MapDatabaseToFolderName(string databaseName)
    {
        var dConfig = _config.Databases.FirstOrDefault(a => a.Name.ToLower() == databaseName.ToLower());
        var name = dConfig?.FolderName;
        if (string.IsNullOrEmpty(name))
            name = databaseName.StripBracket();
        return name;
    }


    private T GetSqlObject<T>(Urn urn, Func<string, string, T> func)
    {
        var name = urn.GetAttribute("Name");
        var schema = urn.GetAttribute("Schema");
        var obj = func.Invoke(name, schema);

        if (obj == null)
            _logger.Warning("SQL object or the dependencies doesn't exist:  {Schema}.{Name}", schema, name);
        return obj;
    }

    private DependencyCollection CreateDependencyCollection(HashSet<Urn> urns)
    {
        var depWalker = new DependencyWalker(_server);
        var tree = depWalker.DiscoverDependencies(urns.ToArray(), true);
        var depCollection = depWalker.WalkDependencies(tree);
        return depCollection;
    }
    void GenerateFile(ScriptSchemaObjectBase sqlObject, string targetPath, Action<ScriptingOptions> callback)
    {
        if (sqlObject == null) return;
        var path = $"{targetPath}/{sqlObject.Schema}.{sqlObject.Name}.sql";
        CreateDirectoryFirst(path);
        _logger.Information("Generating file: {Path}", path);
        var scriptOptions = new ScriptingOptions
        {
            ScriptForCreateDrop = true,
            Indexes = true,
            DriAllConstraints = true,
            ExtendedProperties = true,
            FileName = path
        };
        callback.Invoke(scriptOptions);
    }

    void GenerateSchema(string schemaName)
    {
        var split = schemaName.Split(".");
        var databaseName = split[0];
        var name = split[1];
        var path = $"{_rootPath}{MapDatabaseToFolderName(databaseName)}/Schemas/{name}.sql";
        CreateDirectoryFirst(path);
        _logger.Information("Generating file: {Path}", path);
        using var file = File.CreateText(path);
        file.WriteLine("CREATE SCHEMA " + name);
        file.Close();
    }

    private static void CreateDirectoryFirst(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);
    }
}