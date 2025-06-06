# Pomodoro Timer Web Application

This repository contains a sample ASP.NET Core MVC application with a Pomodoro style timer and accompanying Playwright tests.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/) installed

## Building the application

```
cd myPomodoro
dotnet build
```

## Running the web application

First configure a SQL Server connection string. The application reads the value from the `DefaultConnection` configuration key. You can supply it using environment variables:

```
export DOTNET_ConnectionStrings__DefaultConnection="<your connection string>"
```

Then run the app:

```
dotnet run --project myPomodoro/WebApplication1
```

## Running the tests

```
dotnet test
```

Playwright may need browsers installed the first time you run the tests:

```
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

