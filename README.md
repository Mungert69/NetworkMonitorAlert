# NetworkMonitorAlert

## Overview
`NetworkMonitorAlert` listens to alert-related RabbitMQ exchanges and dispatches
notifications (email/webhook) for monitor and predict pipelines. It is responsible for:

- Consuming monitor & predict status updates (`alertUpdateMonitorStatusAlerts`,
  `alertUpdatePredictStatusAlerts`).
- Managing alert state (which users have been notified, reset queues, etc.).
- Sending email notifications via the configured SMTP provider.
- Exposing wake-up/init endpoints for other services to trigger (`alertMessageInit`,
  `alertServiceReady`, `serviceWakeUp`).

## Architecture
```
NetworkMonitorAlert/
 ├── Services/
 │   ├── AlertMessageService  // orchestrates alert flows
 │   ├── DataQueueService     // validates AuthKeys + merges alert payloads
 │   └── RabbitListener       // binds to all alert exchanges
 ├── Program.cs / Startup.cs  // Host + DI + Rabbit configuration
 ├── Templates/               // Email templates (HTML + text)
 ├── TestData/                // Sample payloads for integration tests
 └── Dockerfile / build-run   // Container utilities
```

## Auth & security
- **ServiceID / ServiceAuthKey** – each producer must publish alerts using a ServiceID
  string and an encrypted token (AuthKey). `DataQueueService` decrypts the key with
  `EmailEncryptKey` and checks it matches the AppID. Use the helper in
  `NetworkMonitorAuthKeyGen` to generate new tokens.
- **EmailEncryptKey** – symmetric key used for auth token encryption. Stored in `.env`
  (and consumed via `securefiles/dev/appsettings-*.json`).

## Prerequisites
- .NET 9 SDK
- SMTP access (configured in `appsettings*.json`)
- RabbitMQ 4.x with the exchanges declared by `RabbitListener`
- Shared state directory mounted at `/app/state` (host path
  `securefiles/dev/state/` in dev)

## Configuration
- Base settings are in `appsettings.json`.
- Environment-specific overrides live in `securefiles/dev/appsettings/`.
- Secrets such as `EmailEncryptKey`, SMTP password, and ServiceAuthKey are injected via
  environment variables (`.env` or container configuration).

## Running locally
```bash
dotnet restore
dotnet run --project NetworkMonitorAlert.csproj
```
The service will connect to RabbitMQ using the connection defined in `LocalSystemUrl`.

To run inside the dev docker environment:
```bash
./build-run            # build + run linux-x64 container
# or
docker compose up alert
```

## Testing
```bash
dotnet test
```
Tests are located in `Tests/` and use sample payloads from `TestData/`.

## Operational notes
- Alert state is cached in `/app/state` (`UserInfos`, `ProcessorList`, etc.). Restarting
  the container reads the existing files; to refresh from the DB, publish an
  `alertMessageInit` message with `UpdateUserInfos=true`.
- If you see `Failed CommitProcessorDataBytes bad AuthKey`, ensure the producer and
  this service share the same ServiceID/AuthKey pair.
- SMTP failures are logged at warning level; enable debug logging for
  `NetworkMonitor.Alert.Services.AlertMessageService` to trace email content.

## Related services
- **NetworkMonitorService** – publishes monitor alert payloads.
- **NetworkMonitorML (Predict)** – publishes predict alert payloads.
- **NetworkMonitorScheduler** – triggers periodic wake-ups (Monitor/Predict checks).
- **NetworkMonitorAuthKeyGen** – helper to generate valid ServiceAuthKey tokens.

