using System;
using System.Collections.Generic;
using System.Linq;

namespace Siganberg.SqlGen;

public class CommandList
{
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
        commandList.Commands = args.Skip(1).Select(a =>
        {
            var value = a.StripBracket().Split(".");

            switch (value.Length)
            {
                case < 2:
                    throw new ArgumentException($"Invalid value {value}." + " Format should be {dbName}.{schema}.{name}. If {dbName} is not specified, it will try to use the first dbName on the sqlgen.json");
                case 3:
                    return new Command
                    {
                        DbName = value[0],
                        Schema = value[1],
                        Name = value[2]
                    };
                default:
                    return new Command
                    {
                        Schema = value[0],
                        Name = value[1]
                    };
            }
        }).ToList();
        
        return commandList;
    }

  
}