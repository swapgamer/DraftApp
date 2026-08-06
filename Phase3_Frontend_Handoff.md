# Phase 3 Frontend Handoff

## Current state

The Angular frontend is in `UI/DraftAOD`; the .NET 8 API is in `API/DraftDatastore`.

Implemented features include public pages, registration/sign-in, guarded routes, Hall of Fame search/filter/sort/pagination, grid and table result views, player profiles, global search, assistant chat, user profile, theme selection, admin dashboard/user/activity pages, error pages, loading and notification infrastructure.

Recent quality work added persistent assistant history (last 30 turns), keyboard-accessible Hall table links, player profile error/reload handling, and test coverage for player loading and route guards.

## Run locally

Start the API from `API/DraftDatastore`:

```powershell
dotnet run --project DraftDatastore.API --launch-profile https
```

The development API is available at `http://localhost:5015`; Swagger is at `http://localhost:5015/swagger`.

Start the frontend from `UI/DraftAOD`:

```powershell
npm install
npm start
```

Open `http://localhost:4200`. The frontend environment targets `http://localhost:5015/api/v1`.

## Verification completed

```powershell
cd UI/DraftAOD
npm exec tsc -- -p tsconfig.app.json --noEmit
npm test -- --watch=false --browsers=ChromeHeadless
npm run build
```

- Unit tests: 11 passing.
- Production build: passing.
- Manual browser QA: home and protected guest routing work with no browser-console errors.
- Build warning: initial bundle is about 682 kB, exceeding the configured 500 kB budget.

## Key implementation decisions

| Decision | Why | Alternative rejected | Scalability note |
|---|---|---|---|
| Angular signals for local UI state | Small, explicit reactive state with OnPush components | Global store would add unnecessary indirection now | A feature store can be added when cross-feature state grows |
| Lazy-loaded feature routes | Keeps individual feature code separate | Eager route imports increase initial work | Supports independently growing admin and public areas |
| HTTP interceptors for auth/loading/retry/errors | Enforces consistent API behavior centrally | Per-component HTTP handling duplicates policy | Retry is GET-only to avoid replaying mutations |
| Server-side pagination/search | Avoids loading the player datastore into the browser | Client-side full dataset filtering | Works as player records grow |
| Local assistant history capped at 30 | Persists useful context without backend schema work | Unlimited local history can consume storage | Move to user-scoped API persistence for multi-device history |

Interview prompts: explain the guarded-route return URL, why GET retries are safe while mutations are not, signal vs RxJS responsibilities, and the trade-off between local and server chat history.

## API limitations blocking remaining UI work

Do not fabricate these features in the frontend. The backend needs the following contracts first:

1. Favorites: authenticated create/list/delete favorite endpoints.
2. Admin player management: nationality/era/position lookup endpoints, response IDs, deleted-player listing, and image upload/storage endpoint.
3. Editable account details: authenticated user profile read/update endpoints.
4. Player media/related players: image URL and a related-player endpoint, or enough ranking/filter metadata to calculate recommendations.

## Remaining Phase 3 work

1. Authenticated browser/API QA using a dedicated local test account.
2. Implement the API contracts above, then build the dependent UI screens.
3. Add position, nationality, and era filter controls when lookup endpoints exist.
4. Perform full responsive and accessibility review on real devices.
5. Reduce the initial bundle below the configured budget; inspect Angular Material imports and defer noncritical app-shell UI where practical.

## Data and database

SQL Server LocalDB instance: `(localdb)\MSSQLLocalDB`.

Database name: `DraftDatastore`.

If migrations must be applied:

```powershell
$env:DRAFT_DATASTORE_CONNECTION_STRING = 'Server=(localdb)\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef database update --project DraftDatastore.Persistence --startup-project DraftDatastore.API
```
