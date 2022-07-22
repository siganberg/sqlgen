# SerilogElasticSearch [![Nuget](https://img.shields.io/nuget/v/Siganberg.SerilogElasticSearch)](https://www.nuget.org/packages/Siganberg.SerilogElasticSearch/) [![Nuget](https://img.shields.io/nuget/dt/Siganberg.SerilogElasticSearch)](https://www.nuget.org/packages/Siganberg.SerilogElasticSearch/)


## Installtion 

**First**, install the Siganberg.SerilogElasticSearch NuGet package into your app.

```console
dotnet add package Siganberg.SerilogElasticSearch
```

**Next**, in your application's _Program.cs_ file, configure Serilog.  


```c#
public class Program
{
    public static void Main()
    {
        var builder = CreateWebHostBuilder(null);
        var host = builder.Build();
            host.Run();
    }

    private static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
        var builder = WebHost.CreateDefaultBuilder(args);
        builder.UseStartup<Startup>()
            .SuppressStatusMessages(true)
            .ConfigureAppConfiguration(ConfigureConfiguration)
            .UseSerilog((hostingContext, loggerConfiguration) =>
            {
                loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration);
                if (hostingContext.HostingEnvironment.IsDevelopment())
                    loggerConfiguration.WriteTo.Console();
                else
                    loggerConfiguration.WriteTo.Console(new ElasticSearchFormatter());
            });

        return builder;
    }

    private static void ConfigureConfiguration(WebHostBuilderContext hostingContext, IConfigurationBuilder config)
    {
        var env = hostingContext.HostingEnvironment;
        config.SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true)
            .AddEnvironmentVariables();
    }
}
```

On `Startup.cs`, add  `app.UseRequestLogging()` as the first line or before any middelware on the Configure method. 

```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app.UseRequestLogging();

    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    app.UseMvc();
}
```

And for `ConfigureService` method. 

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.
        //-- Optional RequestLoggingOptions 
        .AddSingleton<IRequestLoggingOptions, CreateYourRequestLoggingOptionsClass>()
        .AddMvc();
}
```

**Then**, add `appsettings.json` configuration.

```json
{
    "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "Enrich": [
      "FromLogContext"
    ]
  },
  "RequestLoggingOptions" : {
      "IncludeResponseBody" : "false",
      "ExcludeHeaderNames" :[
        "Authorizations"
      ]
  }
}
```

### Notes: 

*Use `System.Net.Http.HttpClient.Default.LogicalHandler` to override HttpClient downstream call logs*. 

### Configuration


*RequestLoggingOptions*

| Property | Default | Descriptions                                                                                                                                       |
|---------------------|---------|----------------------------------------------------------------------------------------------------------------------------------------------------|
|     IncludeResponseBody                | false   | When true, it will add middleware to capture and add response body to the RequestLogging. This add overhead and should only use for troubleshooting if necessary.  |                                                                                                                                               |
|     IncludeRequestHeaders                | true   | Include Request Headers.  |
|     ExcludeHeaderNames                | empty   | Array of string of header names. Ignored if **IncludeRequestHeaders** is set to **false**. Header name will still be log but value is set to *\<OMITTED\>*  |
## Controlling RequestLogging dynamically 

You can also dynamically control when to inject request logging by implementing `IRequestLoggingOptions` and register it using dependency injection. 

*Note: This is only applicable for normal request flow. When ERROR occured, request logging are always injected since the goal of this logger is to get as much as information when something goes wrong.*

```c#
public class RequestLoggingOptions : IRequestLoggingOptions
{
    private readonly List<string> _excludedPaths = new List<string>
    {
        "healthz",
        "swagger"
    };

    public bool IncludeRequestWhen(HttpContext context)
    {
        var path = context.Request.Path.ToString();
        if (_excludedPaths.Any(a => path.Contains(a)))
            return false;

        //-- TODO: Add more logic here.  
        //-- example: Evaluate LaunchDarkly flag to turn ON/OFF RequestLogging based on some flag.

        return true;
    }    
}
  ```
# sqlgen
