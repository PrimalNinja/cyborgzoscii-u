REM dotnet nuget push bin/NuGet/CyborgUnicorn.UNINTELLIGENCE.*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
dotnet pack CyborgUnicorn.UNINTELLIGENCE.csproj -c Release -o bin/NuGet
