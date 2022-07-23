using System.IO;
using System.Text.Json;
using Serilog;

namespace Siganberg.SqlGen;

public class ConfigurationService
{
    public  SqlGenConfig GetSqlGenConfig()
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