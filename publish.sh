#!/bin/bash
dotnet publish -c Release
dotnet nuget push Siganberg.SerilogElasticSearch/bin/Release/Siganberg.SerilogElasticSearch.$1.nupkg -k $NUGETKEY -s "https://api.nuget.org/v3/index.json"
