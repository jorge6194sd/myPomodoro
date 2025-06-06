# MyPomodoro

This repository contains a sample ASP.NET Core application.

## Connection string

`Program.cs` reads the database connection string using:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
```

Both `appsettings.json` and `appsettings.Development.json` contain empty values for `ConnectionStrings:DefaultConnection`. Before running the application you must provide a value through one of the following methods:

1. **Environment variable**

   ```bash
   export DOTNET_ConnectionStrings__DefaultConnection="<your connection string>"
   ```

2. **User secrets**

   Inside the `WebApplication1` project directory, run:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>"
   ```

After setting the connection string you can run the application normally.
