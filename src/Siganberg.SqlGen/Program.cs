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

var name = Environment.GetCommandLineArgs();
generator.Run(name.Length > 1 ? name[1] : string.Empty);