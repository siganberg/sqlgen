using System;
using System.Collections.Generic;

namespace Siganberg.SqlGen;

public class CommandList
{
    public string Server { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string TargetPath { get; set; }
    public List<Command> Commands { get; set; }

    public CommandList()
    {
        Commands = new List<Command>();
    }
}

public class Command
{
    public string Type { get; set; }
    public string DbName { get; set; }
    public string Schema { get; set; }
    public string Name { get; set; }
}

public class ArgumentParser
{
    public CommandList Parse(string[] args)
    {
        if (args.Length == 1)
            return null; 
        
        var commandList = new CommandList();
        for (var i = 1; i < args.Length; i+=2)
        {
            var command = args[i].Replace("-", "");
            if (i+1 == args.Length) 
                throw new ArgumentException($"Missing value for command {command}");

            switch (command)
            {
                case "--server":
                    commandList.Server = args[i + 1];
                    break;
                case "--username":
                    commandList.UserName = args[i + 1];
                    break;
                case "--password":
                    commandList.Password = args[i + 1];
                    break;
                case "--targetPath":
                    commandList.TargetPath = args[i + 1];
                    break;
                default:
                {
                    var value = args[i + 1].StripBracket().Split(".");

                    if (value.Length != 3)
                        throw new ArgumentException($"Invalid value {value}." + " Format should be {dbName}.{schema}.{name}");

                    commandList.Commands.Add(new Command
                    {
                        Type = command,
                        DbName = value[0],
                        Schema = value[1],
                        Name = value[2]
                    });
                    break;
                }
            }

        }
        return commandList;
    }


}