using System;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;
using Siganberg.SqlGen;

var config = new ConfigurationService().GetSqlGenConfig();
if (config == null)
    return;

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
try
{
    var argument = new ArgumentParser().Parse(Environment.GetCommandLineArgs());
    var connection = new ServerConnection(config.Server, argument.Username, argument.Password);
    var server = new Server(connection);
    var generator = new GeneratorService(config, server);

    generator.Run(argument);
}
catch (Exception e)
{
    logger.Error("Error enounter. {Message} {InnerMessage}", e.Message, e.InnerException!.Message);
}


