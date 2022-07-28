using System;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Siganberg.SqlGen;

var config = new ConfigurationService().GetSqlGenConfig();
if (config == null)
    return;

var connection = new ServerConnection(config.Server, config.Username, config.Password);
var server = new Server(connection);
var generator = new GeneratorService(config, server);

var argument = new ArgumentParser().Parse(Environment.GetCommandLineArgs());

generator.Run(argument);