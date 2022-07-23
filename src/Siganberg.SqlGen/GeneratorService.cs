using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public void Run()
    {
        _logger.Information("Generating SQL Generator starting...");


        foreach (var d in _config.Databases)
        {
            _logger.Information("Generating database scripts for {Name}", d.Name);
            var urns = new HashSet<Urn>();

            if (ValidateDatabaseInput(d))
                continue;

            var database = GetDatabase(d);
            if (database == null) continue;

            urns.UnionWith(CollectUrn(d.Tables, (name, schema) => database.Tables[name, schema]));
            urns.UnionWith(CollectUrn(d.StoredProcedures, (name, schema) => database.StoredProcedures[name, schema]));
            urns.UnionWith(CollectUrn(d.Views, (name, schema) => database.Views[name, schema]));

            if (urns.Count == 0) continue;
            
            var depCollection = CreateDependencyCollection(_server, urns);
            var schemas = GenerateSqlFiles(depCollection);

            foreach (var schema in schemas)
                GenerateSchema(schema);
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

    private Database GetDatabase(SqlGenConfig.Database d)
    {
        var database = _server.Databases[d.Name];
        if (database == null)
            _logger.Information("Database with name {Name} not found", d.Name);
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
            var database = GetDatabase(dep.Urn);
            if (database == null) continue; 
            switch (dep.Urn.Type)
            {
                case "Table":
                {
                    var obj = GetSqlObject(dep.Urn, (name, schema) => database.Tables[name, schema]);
                    GenerateFile(obj, $"{_rootPath}/{MapDatabaseToFolderName(database.Name)}/Tables", o => obj.Script(o));
                    break;
                }
                case "StoredProcedure":
                {
                    var obj = GetSqlObject(dep.Urn, (name, schema) => database.StoredProcedures[name, schema]);
                    GenerateFile(obj, $"{_rootPath}/{MapDatabaseToFolderName(database.Name)}/StoredProcedures", o => obj.Script(o));
                    break;
                }
                case "View":
                {
                    var obj = GetSqlObject(dep.Urn, (name, schema) => database.Views[name, schema]);
                    GenerateFile(obj, $"{_rootPath}/{MapDatabaseToFolderName(database.Name)}/Views", o => obj.Script(o));
                    break;
                }
            }

            schemas.Add($"{database.Name}.{dep.Urn.GetAttribute("Schema")}");
        }

        return schemas;
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

    private static DependencyCollection CreateDependencyCollection(Server server, HashSet<Urn> urns)
    {
        var depWalker = new DependencyWalker(server);
        var tree = depWalker.DiscoverDependencies(urns.ToArray(), true);
        var depCollection = depWalker.WalkDependencies(tree);
        return depCollection;
    }

    private static List<Urn> CollectUrn(IEnumerable<string> names, Func<string, string, SqlSmoObject> collector)
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
        var path = $"{_rootPath}/{MapDatabaseToFolderName(databaseName)}/Schemas/{name}.sql";
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