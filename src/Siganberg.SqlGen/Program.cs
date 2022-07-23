using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Siganberg.SqlGen;

var config = new ConfigurationService().GetSqlGenConfig();
var connection = new ServerConnection(config.Server, config.Username, config.Password);
var server = new Server(connection);
var generator = new GeneratorService(config, server);
generator.Run();