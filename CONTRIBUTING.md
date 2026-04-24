# Contributing to CBAD

Thank you for your interest in contributing! Below you'll find everything you need to get started.

## Building Locally

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on Windows.

```bash
git clone https://github.com/gwchristen/CBAD.git
cd CBAD
dotnet restore CBAD.sln
dotnet build CBAD.sln --configuration Release
```

To run the application:

```bash
dotnet run --project src/CBAD/CBAD.csproj -- --port COM3 --baud 9600
```

## Opening in Visual Studio

Open `CBAD.sln` at the repo root directly in Visual Studio 2022 (or later). All projects are included in the solution.

## Running Tests

```bash
dotnet test CBAD.sln --configuration Release --verbosity normal
```

## Branch Naming and PR Conventions

- Create a feature branch from `main` with a descriptive name, e.g. `feature/csv-output-improvements` or `fix/reconnect-crash`.
- Keep commits focused and atomic.
- Write a clear, descriptive PR title summarising the change (e.g. "Add auto-reconnect delay option").
- Include a short description in the PR body explaining *what* changed and *why*.
- Ensure `dotnet build` and `dotnet test` pass locally before opening a PR.

## Reporting Issues

When filing a bug report, please use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md) and include:

- Steps to reproduce the problem
- Expected vs. actual behaviour
- Your OS version, .NET version, COM port adapter, and Cadex model

For feature requests, use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).
