# MyPomodoro

This repository contains an ASP.NET Core web application and NUnit/Playwright tests.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/) installed
- Optional: [Playwright CLI](https://playwright.dev/dotnet/docs/intro) for installing browsers (`playwright install`)

## Building the Solution

From the repository root, run:

```bash
 dotnet build myPomodoro/myPomodoro.sln
```

## Running the Web Application

Execute the following to start the site locally:

```bash
 dotnet run --project myPomodoro/WebApplication1
```

The app will be available at the HTTPS URL shown in the console output (e.g., `https://localhost:5001`).

### Connection String

The application reads the SQL connection string from configuration. You can override it with an environment variable before running:

```bash
 export ConnectionStrings__DefaultConnection="<your connection string>"
```

## Running Tests

The solution includes NUnit tests using Playwright. Ensure Playwright browsers are installed (first run of the tests will attempt to install them automatically). Run tests with:

```bash
 dotnet test myPomodoro/TestProject1/TestProject1.csproj
```


