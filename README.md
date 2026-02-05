# BuilderService

A Windows service built on ASP.NET Core (.NET 9) that provides REST APIs for executing PowerShell scripts asynchronously, with task queuing, timeout control, and output streaming.

## Features

- Submit `.ps1` scripts for async execution, get a task ID for tracking
- Query task status (Pending / Running / Completed / Failed / TimedOut / Cancelled)
- Stream stdout/stderr output with offset-based incremental reads
- Stop running tasks
- Auto-cleanup of completed tasks after configurable retention period
- Rate limiting (per-IP fixed window)
- Swagger UI

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows (PowerShell execution)

## Setup

```powershell
.\setup.ps1
```

This installs the required global tool (`reportgenerator`) and restores NuGet packages.

## Publish

```powershell
.\publish.ps1
```

## Run

```powershell
dotnet run
```

The service listens on `http://*:22333` by default (configured in `appsettings.json`).

Swagger UI: `http://localhost:22333/swagger`

## API Endpoints

### Status

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/status` | Health check, returns readiness |

### PowerShell Tasks

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/powershell/run` | Submit a script for execution |
| GET | `/api/powershell` | List all tasks |
| GET | `/api/powershell/{id}` | Get task details |
| GET | `/api/powershell/{id}/output?outputOffset=0&errorOffset=0` | Get task output (incremental) |
| POST | `/api/powershell/{id}/stop` | Cancel a running task |

#### Submit a script

```json
POST /api/powershell/run
{
  "scriptPath": "C:\\scripts\\build.ps1"
}
```

Returns a task ID. Only `.ps1` files are accepted; paths are validated against command injection characters.

#### Get output (incremental)

Use the `outputOffset` and `errorOffset` from the previous response to fetch only new output:

```
GET /api/powershell/{id}/output?outputOffset=120&errorOffset=0
```

## Jenkins Integration

The `tools/JenkinsPipeline/` directory contains example Jenkinsfiles for calling BuilderService from Jenkins pipelines.

### Required Jenkins Plugins

| Plugin | Provides | Usage |
|--------|----------|-------|
| [HTTP Request](https://plugins.jenkins.io/http_request/) | `httpRequest` step | Sends HTTP requests to BuilderService APIs |
| [Pipeline Utility Steps](https://plugins.jenkins.io/pipeline-utility-steps/) | `readJSON` step | Parses JSON responses from the API |

### Example Pipelines

- **`status.jenkinsfile`** — Checks whether the service is ready via `GET /api/Status`.
- **`callsync.jenkinsfile`** — Submits a PowerShell script, then polls for completion while streaming stdout/stderr output.

## Configuration

`appsettings.json`:

```json
{
  "RateLimiting": {
    "PermitLimit": 240,
    "WindowSeconds": 60
  },
  "PowerShellService": {
    "MaxTasks": 100,
    "CompletedTaskRetentionMinutes": 10080,
    "TaskTimeoutMinutes": 1440,
    "OutputDirectory": "task-outputs"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:22333"
      }
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `RateLimiting:PermitLimit` | 240 | Max requests per window per IP |
| `RateLimiting:WindowSeconds` | 60 | Rate limit window in seconds |
| `PowerShellService:MaxTasks` | 100 | Max concurrent + retained tasks |
| `PowerShellService:CompletedTaskRetentionMinutes` | 10080 | How long completed tasks are kept (7 days) |
| `PowerShellService:TaskTimeoutMinutes` | 1440 | Max execution time per task (24 hours) |
| `PowerShellService:OutputDirectory` | `task-outputs` | Directory for persisted task output files |

## Test

```powershell
.\test.ps1
```

Runs unit tests and integration tests with code coverage, then generates an HTML coverage report under `TestResults/CoverageReport/`.

## Publish

```powershell
.\publish.ps1
```

Produces a self-contained single-file executable for `win-x64`.
