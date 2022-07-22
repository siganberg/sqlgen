using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using Serilog;

namespace Siganberg.SqlGen;

public class GeneratorService
{
    private readonly ILogger _logger;

    public GeneratorService()
    {
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    public void Run()
    {
        _logger.Information("Generating SQL Generator starting...");
        
        var config = GetSqlGenConfig();
        var connection = new ServerConnection(config.Server, config.Username, config.Password);
        var server = new Server(connection);
        var targetPath = Directory.GetCurrentDirectory() + "/" + config.TargetPath;

        foreach (var d in config.Databases)
        {
            _logger.Information("Generating database scripts for {Name}", d.Name);
            var urns = new HashSet<Urn>();
            
            if (ValidateDatabaseInput(d)) 
                continue;
            
            var database = GetDatabase(server, d);
            var databasePath = SetupTargetLocation(targetPath, d);

            urns.UnionWith(CollectUrn(d.Tables, (name, schema) => database.Tables[name, schema]));
            urns.UnionWith(CollectUrn(d.StoredProcedures, (name, schema) => database.StoredProcedures[name, schema]));
            urns.UnionWith(CollectUrn(d.Views, (name, schema) => database.Views[name, schema]));

            var depCollection = CreateDependencyCollection(server, urns);
            var schemas = GenerateSqlFiles(depCollection, database, databasePath);

            foreach (var schema in schemas)
                GenerateSchema(schema, databasePath);
        }
        
        _logger.Information("Generation completed!");

    }

    private bool ValidateDatabaseInput(SqlGenConfig.Database d)
    {
        if (string.IsNullOrEmpty(d.Name))
        {
            _logger.Information("Database name cannot be empty");
            return true;
        }

        return false;
    }

    private Database GetDatabase(Server server, SqlGenConfig.Database d)
    {
        var database = server.Databases[d.Name];
        if (database == null)
        {
            _logger.Information("Database with name {Name} not found", d.Name);
            return database;
        }

        return database;
    }

    private HashSet<string> GenerateSqlFiles(DependencyCollection depCollection, Database database, string targetPath)
    {
        var schemas = new HashSet<string>();

        foreach (var dep in depCollection)
        {
            switch (dep.Urn.Type)
            {
                case "Table":
                {
                    var obj = database.Tables[dep.Urn.GetAttribute("Name"), dep.Urn.GetAttribute("Schema")];
                    GenerateFile(obj, targetPath + "/Tables", o => obj.Script(o));
                    break;
                }
                case "StoredProcedure":
                {
                    var obj = database.StoredProcedures[dep.Urn.GetAttribute("Name"), dep.Urn.GetAttribute("Schema")];
                    GenerateFile(obj, targetPath + "/StoredProcedures", o => obj.Script(o));
                    break;
                }
                case "View":
                {
                    var obj = database.Views[dep.Urn.GetAttribute("Name"), dep.Urn.GetAttribute("Schema")];
                    GenerateFile(obj, targetPath + "/Views", o => obj.Script(o));
                    break;
                }
            }

            schemas.Add(dep.Urn.GetAttribute("Schema"));
        }

        return schemas;
    }

    private static DependencyCollection CreateDependencyCollection(Server server, HashSet<Urn> urns)
    {
        var depWalker = new DependencyWalker(server);
        var tree = depWalker.DiscoverDependencies(urns.ToArray(), true);
        var depCollection = depWalker.WalkDependencies(tree);
        return depCollection;
    }

    private static List<Urn> CollectUrn(IEnumerable<string> names,  Func<string, string, SqlSmoObject> collector)
    {
        var urns = new List<Urn>();
        foreach (var name in names)
        {
            var split = name.Split(".");
            var sqlObject = collector.Invoke(split[1].StripBracket(), split[0].StripBracket());
            if (sqlObject == null) continue;
            urns.Add(sqlObject.Urn);
        }
        return urns;
    }

    string SetupTargetLocation(string targetPath, SqlGenConfig.Database database)
    {
        var databaseName = string.IsNullOrEmpty(database.FolderName) ? database.Name : database.FolderName;
        var databasePath = targetPath + $"/{databaseName}";
    
        CreateDirectoryIfNotExist(databasePath + "/Tables");
        CreateDirectoryIfNotExist(databasePath + "/StoredProcedures");
        CreateDirectoryIfNotExist(databasePath + "/Schemas");
        CreateDirectoryIfNotExist(databasePath + "/Views");

        return databasePath;
    }

    static void CreateDirectoryIfNotExist(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path!);
    }


    void GenerateFile(ScriptSchemaObjectBase storedProcedure, string targetPath, Action<ScriptingOptions> callback )
    {
        var path = $"{targetPath}/{storedProcedure.Schema}.{storedProcedure.Name}.sql";
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
    
    void GenerateSchema(string schemaName, string targetPath)
    {
        var path = $"{targetPath}/Schemas/{schemaName}.sql";
        _logger.Information("Generating file: {Path}", path);
        using var file = File.CreateText(path);
        file.WriteLine("CREATE SCHEMA " + schemaName);
        file.Close();
    }

    private static SqlGenConfig GetSqlGenConfig()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var jsonConfig = currentDirectory + "/" + "sqlgen.json";
        if (!File.Exists(jsonConfig))
        {
            Log.Logger.Information("sqlgen.json cannot be found");
        }

        var configFile = File.ReadAllText(jsonConfig);
        var configModel = JsonSerializer.Deserialize<SqlGenConfig>(configFile);
        if (configModel == null)
        {
            Log.Logger.Information("sqlgen.json has invalid content");
        }
        return configModel;
    }
}