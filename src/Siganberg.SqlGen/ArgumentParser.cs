using System;
using System.Collections.Generic;

namespace Siganberg.SqlGen;

public class CommandList
{
    public string Username { get; set; }
    public string Password { get; set; }
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
            var command = args[i];
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Missing value for command : {command}");
            
            var rawValue = args[i + 1];
            switch (command)
            {
                case "-u":
                    commandList.Username = rawValue;
                    break;
                case "-p":
                    commandList.Password = rawValue;
                    break;
                case "-o":
                    commandList.Commands.Add(CreateCommand(rawValue));
                    break;
            }
        }

        if (string.IsNullOrEmpty(commandList.Username))
            commandList.Username = Environment.GetEnvironmentVariable("SQLGEN_USERNAME");
        
        if (string.IsNullOrEmpty(commandList.Password))
            commandList.Password = Environment.GetEnvironmentVariable("SQLGEN_PASSWORD");

        if (string.IsNullOrEmpty(commandList.Username))
            throw new ArgumentException("Username was not specified. Please pass -u {username} parameter or add environment variables SQLQGEN_USERNAME.");
        
        if (string.IsNullOrEmpty(commandList.Password))
            throw new ArgumentException("Password was not specified. Please pass -p {username} parameter or add environment variables SQLGEN_PASSWORD.");
        
        return commandList;
    }

    private static Command CreateCommand(string a)
    {
        var value = a.StripBracket().Split(".");

        switch (value.Length)
        {
            case < 2:
                throw new ArgumentException($"Invalid value {value}." +
                                            " Format should be {dbName}.{schema}.{name}. If {dbName} is not specified, it will try to use the first dbName on the sqlgen.json");
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
    }
}