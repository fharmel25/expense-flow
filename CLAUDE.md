# ExpenseFlow – CLAUDE.md

## What we're building

**ExpenseFlow** is a **personal project**: a cross-platform expense approval app (web, Android and iOS from one React Native codebase).

- **Employees** log in, photograph a receipt, submit an expense, and track its status.
- **Managers** see pending expenses from their team and approve or reject them (rejecting requires a reason).
- Every decision triggers a **push notification**.
- The approval process is modelled as an **Elsa workflow**: Submitted → Manager Review (waits) → Approved / Rejected → Notify.
- **Monthly reports** run on PostgreSQL. They are tuned with indexes and checked with EXPLAIN ANALYZE, keeping before/after timings.
- Everything is deployed to **Azure**, with an **Azure DevOps** CI/CD pipeline.

### End-to-end flow

1. The app logs in through the API and receives a JWT. It stores the token in SecureStore on mobile and in browser storage on web.
2. The app asks the API for a write-only **SAS URL**, uploads the receipt photo **directly** to Blob Storage, then calls `POST /expenses`.
3. The API saves the expense (Submitted) and starts an Elsa workflow. The workflow sets InReview and asks the Node service to notify the manager.
4. The manager approves or rejects. The API resumes the workflow, which sets the final status and notifies the employee.

### Repository layout (planned)

```
ExpenseFlow/
├─ api/             .NET 8 solution (Clean Architecture)
│  ├─ src/ExpenseFlow.Domain
│  ├─ src/ExpenseFlow.Application
│  ├─ src/ExpenseFlow.Infrastructure
│  ├─ src/ExpenseFlow.Api
│  └─ tests/ExpenseFlow.UnitTests, tests/ExpenseFlow.IntegrationTests
├─ notifications/   Node.js + TypeScript + Express service (Expo Push API)
├─ app/             Expo app (Expo Router, TypeScript). Expo slug: "expenseflow"
├─ pipelines/       Azure DevOps YAML pipelines
└─ CLAUDE.md
```

`app/` → Expo owner `fharmels-team`, slug `expenseflow`, project ID `48edb5ed-a99c-4de6-b3c2-c1e32ace2cd7` (https://expo.dev/accounts/fharmels-team/projects/expenseflow)

### Main data model

- **Users:** Role (Employee/Manager), ManagerId.
- **Expenses:** Amount decimal(12,2), Category, Status, ReceiptBlobName, WorkflowInstanceId, DecidedBy/At, RejectionReason.
- **ExpenseHistory:** audit trail of status changes.
- **PushTokens:** UserId, ExpoPushToken, Platform.
- **RefreshTokens:** optional.
- Elsa tables live in their own `elsa` schema.

## Stack

| Area | Technology |
|---|---|
| App | React Native, **Expo** (managed workflow), **Expo Router**, TypeScript, expo-image-picker, expo-image-manipulator, expo-secure-store, expo-notifications, expo-dev-client, TanStack Query |
| API | **.NET 8**, ASP.NET Core Web API, Clean Architecture, MediatR, FluentValidation, JWT Bearer auth, Swagger |
| Data | **Entity Framework Core** + Npgsql, **PostgreSQL** |
| Workflow | **Elsa Workflows 3**, persisted with EF Core on PostgreSQL |
| Notifications | **Node.js** (LTS), TypeScript, Express, expo-server-sdk → Expo Push API → FCM (Android) / APNs (iOS) |
| Files | **Azure Blob Storage** (private container, SAS URLs); **Azurite** locally |
| Cloud | Azure App Service (API + Node), Azure Database for PostgreSQL Flexible Server, Azure Static Web Apps (web build), Key Vault, Application Insights |
| CI/CD | **Azure DevOps** (Repos, YAML pipelines, variable groups), **EAS Build** for mobile |

### Development environment

- **Windows 11 + PowerShell.** No Mac.
- Test device: a **Samsung (Android)**. I use Expo Go first, then my own Android **development build**, because push notifications don't work in Expo Go on Android since SDK 53.
- **Web:** the browser, by pressing `w` in Expo.
- **iOS:** checked with an EAS **iOS simulator build** run on **Appetize.io**. That simulator has no camera and no real push. The Apple Developer account is optional.

## How I want you to work

- **I am a .NET / C# developer. I am new to Node.js, React and React Native.** Explain simply, step by step, and say *why* before *how*. Compare with C#/.NET when it helps (npm ≈ NuGet, Promise ≈ Task, package.json ≈ .csproj…).
- **Show me the code in the chat first, with a detailed explanation, and wait for my OK before writing any file.** Work in small steps, explain what each new file is for and where it goes, and tell me how to check that it works.
- Follow the project roadmap: one phase at a time, ending with a **checkpoint** I can verify (Postman call, screen on the phone, etc.).
- **Give every command for Windows PowerShell.**
  - Don't use bash syntax.
  - Use `;` to chain commands, not `&&`, because Windows PowerShell 5.1 doesn't support it.
  - Use backtick `` ` `` for line continuation.
- Keep the **Clean Architecture** rule: dependencies point inward (Api → Application → Domain; Infrastructure implements Application interfaces). Business rules belong in Domain/Application, not in controllers or workflow wiring.
- **Install Expo libraries with `npx expo install`**, not plain `npm install`, so versions stay compatible with the SDK.
- **Ask me before:**
  - adding a new library;
  - changing the architecture;
  - changing the database schema;
  - doing anything that costs money on Azure or Apple.
- **Never put secrets in code or Git.** Use `dotnet user-secrets` for the API, `.env` files for Node and the app (git-ignored, with `.env.example` committed), and Key Vault / App Service settings in Azure.
- Store dates in **UTC** (Npgsql requirement).
- Keep answers concise. Point out things that are useful for my CV or interviews (for example, measured performance gains).
- When something fails, explain the error message in plain words first, then the fix.

## Setup, run and test commands (Windows PowerShell)

> Commands assume you start in the repo root `C:\AI\ExpenseFlow`. Folder and project names follow the planned layout above; adjust them if they change.

### One-time machine setup

```powershell
# Check prerequisites
dotnet --version        # 8.x
node -v                 # v20+ (LTS)
npm -v
git --version

# If PowerShell blocks npm/npx scripts ("running scripts is disabled")
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# Global tools
dotnet tool install --global dotnet-ef --version 8.*
npm install -g eas-cli azurite

# Log in to Expo
eas login
```

### Local infrastructure

```powershell
# PostgreSQL in Docker (or use a local PostgreSQL install)
docker run --name expenseflow-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=expenseflow -p 5432:5432 -d postgres:16
docker start expenseflow-db      # on later days

# Azurite (local Blob Storage), keeps its data in .azurite (git-ignored)
azurite --silent --location .azurite

# Your PC's local IP, which the phone needs to reach the API (look for "IPv4 Address")
ipconfig

# Allow the phone to reach the API port (run PowerShell as Administrator, once)
New-NetFirewallRule -DisplayName "ExpenseFlow API 5080" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow
```

### API (.NET 8)

```powershell
cd api

dotnet restore
dotnet build

# Secrets (once)
dotnet user-secrets init --project src/ExpenseFlow.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=expenseflow;Username=postgres;Password=postgres" --project src/ExpenseFlow.Api
dotnet user-secrets set "Jwt:Key" "<a-long-random-secret>" --project src/ExpenseFlow.Api
dotnet user-secrets set "Notifications:InternalApiKey" "<shared-secret>" --project src/ExpenseFlow.Api

# EF Core migrations
dotnet ef migrations add <MigrationName> --project src/ExpenseFlow.Infrastructure --startup-project src/ExpenseFlow.Api
dotnet ef database update --project src/ExpenseFlow.Infrastructure --startup-project src/ExpenseFlow.Api
dotnet ef migrations script --idempotent -o migrate.sql --project src/ExpenseFlow.Infrastructure --startup-project src/ExpenseFlow.Api

# Run, listening on all addresses so the phone can reach it (Swagger: http://localhost:5080/swagger)
dotnet run --project src/ExpenseFlow.Api --urls "http://0.0.0.0:5080"
dotnet watch --project src/ExpenseFlow.Api --urls "http://0.0.0.0:5080"   # auto-reload

# Tests
dotnet test
dotnet test --filter "FullyQualifiedName~UnitTests"
```

### Notification service (Node.js)

```powershell
cd notifications

npm install
Copy-Item .env.example .env      # then fill in PORT, INTERNAL_API_KEY

npm run dev      # development with auto-reload
npm run build    # compile TypeScript to dist/
npm start        # run compiled build
npm test

# Health check
Invoke-RestMethod http://localhost:4000/health

# Send a test push to a real device token
Invoke-RestMethod -Method Post -Uri http://localhost:4000/notify `
  -Headers @{ "x-internal-key" = "<shared-secret>" } `
  -ContentType "application/json" `
  -Body '{"tokens":["ExponentPushToken[xxxx]"],"title":"Test","body":"Hello from ExpenseFlow","data":{"expenseId":"1"}}'
```

### App (Expo)

```powershell
cd app

npm install
Copy-Item .env.example .env      # EXPO_PUBLIC_API_URL=http://<your-PC-IP>:5080

npx expo start                   # scan the QR code from inside Expo Go / the dev build on the Samsung
npx expo start --tunnel          # if the phone can't connect over Wi-Fi
npx expo start -c                # clear the cache when things behave strangely
# In the Expo terminal: press w = web, r = reload, Ctrl+C = stop

npx expo install <package-name>  # add an Expo-compatible library
npx expo-doctor                  # check project health and dependency versions
npx tsc --noEmit                 # TypeScript type check
npx expo lint                    # lint

npx expo export --platform web   # web production build, output in dist/
```

### Mobile builds (EAS, in the cloud)

```powershell
cd app

eas init                                               # already done: links to the Expo project
eas build:configure                                    # creates eas.json
eas credentials                                        # manage FCM (Android push) / Apple credentials

eas build --profile development --platform android     # dev build for the Samsung (needed for push)
eas build --profile preview --platform android         # installable APK to share
eas build --profile ios-simulator --platform ios       # iOS simulator build for Appetize.io (no Apple account)
```

> `eas.json` needs a build profile named `ios-simulator` with iOS simulator builds turned on. Android push also needs `google-services.json` from Firebase, referenced in `app.json`; its Android package name must match the one registered in Firebase.

### Manual end-to-end check

1. Start PostgreSQL, Azurite, the API, the notification service and Expo.
2. Log in as an **employee** on the Samsung, take a receipt photo and submit it. The status should show **In Review**.
3. The **manager** receives a push notification. Log in as the manager on the **web** and approve.
4. The employee receives a push notification, and the status shows **Approved**.
5. Repeat the screens on the **iOS simulator (Appetize)**, using a gallery image.
