# Northwind Support Desk

Northwind Support Desk is a support ticket management system built with
.NET 10 Web API, React, TypeScript, and SQL Server.

This repository was updated as part of the technical assessment. The
implementation covers server-side ticket filtering, ticket triage and
assignment, SLA evaluation and escalation, JWT agent authentication, and
a Docker Compose development environment for the frontend, API, and SQL
Server database.

------------------------------------------------------------------------

## Assignment Status

| Task | Description | Status |
| --- | --- | --- |
| Task 1 | Defect fix and server-side filtering | Completed |
| Task 2 | Ticket triage, assignment, SLA, and escalation | Completed |
| Task 3 | Login and authentication | Completed |
| Task 4 | Dockerise the application and API | Completed and locally verified |

Task 4 was verified locally using Docker Compose. The API connected to
SQL Server, checked the database migrations, and started successfully.
The frontend was served by Nginx and returned HTTP 200. Login and the
main ticket workflows were checked through the containerised frontend.
This is a local development setup, not a production deployment.

------------------------------------------------------------------------

## Task 1: Defect Fix and Server-Side Filtering

**Status: Completed**

### Problem and Root Cause

The ticket list did not consistently apply selected filters. Some
frontend filter changes did not trigger a new API request immediately.
The ticket-list hook had an incomplete effect dependency list, and
search debouncing was applied too broadly instead of only to the search
input.

The backend query also needed to apply filters before calculating the
total number of matching tickets and retrieving the requested page.

### Implementation

-   Added server-side filtering for status, priority, category,
    customer, assigned agent, and unassigned tickets.
-   Added search support for ticket titles, references, and customer
    information.
-   Trimmed search input to handle leading and trailing whitespace.
-   Applied filters before calculating the total result count.
-   Applied sorting and pagination to the filtered result set.
-   Updated the frontend ticket-list hook so filter changes trigger new
    API requests.
-   Applied a 300 ms debounce to the search input only. Other filters
    update immediately.
-   Added or updated regression tests.

### Design Decisions

-   **Server-side filtering:** The backend filters the complete dataset
    rather than only the records currently displayed in the browser.
-   **Filtering before pagination:** The returned records and total
    count represent the same filtered result set.
-   **Debounced search:** Only free-text search is debounced to reduce
    unnecessary requests while typing.

### Testing

Run the test suite with:

``` bash
npm test
```

The test result recorded after Task 2 was 49 backend tests and 3
frontend tests. Later tests were added during Task 3; see the testing
section below for the latest recorded result.

### Related Commits

-   `e82a1d2` --- `fix: apply ticket list filters before pagination`
-   `d55cda8` --- `fix: apply ticket filters immediately`
-   `2528e4f` --- `docs: update README for Task 1`

------------------------------------------------------------------------

## Task 2: Ticket Triage, Assignment, and SLA Escalation

**Status: Completed**

### Objective

Implement automatic ticket triage, agent assignment, SLA calculation,
escalation, escalation history, and frontend support for these features.

### Ticket Priority and Triage

-   The requested priority is used as the default.
-   Categories configured to force Critical priority override the
    requested priority.
-   The triage result includes reasons explaining the decisions.
-   A dedicated triage service keeps the business rules separate from
    the ticket creation handler.

### Automatic Agent Assignment

The assignment process selects an eligible agent based on availability,
specialisation, and workload.

-   Only active agents are considered.
-   Categories requiring specialist handling can only be assigned to
    agents with the appropriate specialisation.
-   Agents who have reached their configured maximum open-ticket
    capacity are excluded.
-   The eligible agent with the fewest open tickets is preferred.
-   A deterministic tie-break is used when agents have the same
    workload.
-   If no eligible agent is available, the ticket remains unassigned and
    the result explains why.

### SLA Calculation

The base SLA window depends on ticket priority.

  Priority     Base SLA
  ---------- ----------
  Critical      4 hours
  High          8 hours
  Medium       24 hours
  Low          72 hours

Premium customers receive half of the standard SLA window. A minimum
window of one hour is applied. SLA values and the at-risk threshold are
configurable through application settings.

The calculated SLA due date is included in the ticket information.

### SLA Evaluation

The evaluator supports these statuses:

-   `NotApplicable`
-   `WithinSla`
-   `AtRisk`
-   `Breached`
-   `Met`

A configurable threshold determines when a ticket is considered at risk.
An SLA status filter is available in the ticket list.

### Ticket Escalation

-   The escalation reason must contain between 5 and 500 characters.
-   Resolved and closed tickets cannot be escalated.
-   A ticket already at Critical priority cannot be escalated further.
-   Priority increases one level at a time: Low → Medium → High →
    Critical.
-   The SLA due date is recalculated based on the new priority and
    customer tier.
-   Each escalation is recorded in escalation history.
-   The escalation actor is derived from the authenticated user's claims
    rather than trusted from client-supplied identity data.

The implementation includes the escalation domain model, database
migration, repository, and command handler.

### API Endpoints

  ---------------------------------------------------------------------------------
  Method                  Endpoint                          Purpose
  ----------------------- --------------------------------- -----------------------
  `POST`                  `/api/tickets/{id}/escalate`      Escalate a ticket

  `GET`                   `/api/tickets/{id}/escalations`   Retrieve escalation
                                                            history
  ---------------------------------------------------------------------------------

### Frontend Implementation

-   An escalation panel on the ticket detail page.
-   A form for submitting an escalation reason.
-   Client-side validation and success/error feedback.
-   A view of escalation history.
-   An SLA status filter on the ticket list.
-   API client methods and TypeScript types for escalation requests and
    responses.

### Design Decisions

-   **Separate triage service:** Priority calculation and agent
    assignment are handled by a dedicated service.
-   **Configurable SLA settings:** SLA windows and the at-risk threshold
    are stored in configuration.
-   **Workload-based assignment:** The eligible agent with the lowest
    open-ticket count is preferred while respecting capacity.
-   **Escalation history:** Escalations are stored as separate history
    records.
-   **Backend validation:** The API validates escalation requests
    independently of frontend validation.
-   **SLA filtering:** SLA status is passed to the backend query so the
    filter applies to the dataset, not only the visible records.

### Related Commits

-   `d09d871` --- `feat: implement ticket triage and SLA escalation`
-   `3040d76` --- `feat: add ticket escalation UI and SLA filtering`
-   `2604fb7` --- `docs: update README and add implementation approach`

------------------------------------------------------------------------

## Task 3: Authentication and Login

**Status: Completed**

### Implementation

-   Agent passwords are stored as password hashes using ASP.NET Core's
    `IPasswordHasher<Agent>`.
-   Development seeding adds password hashes to seeded agents that do
    not already have one.
-   Login checks that the agent exists, is active, and has a valid
    password.
-   Successful login returns a signed JWT, its expiry time, and basic
    agent details.
-   Protected API endpoints require a valid bearer token.
-   `GET /api/auth/me` returns the currently authenticated agent.
-   The frontend stores the login session in `sessionStorage`.
-   The frontend sends the bearer token with protected API requests.
-   Protected routes redirect unauthenticated users to the login page.
-   Logout clears the stored session.
-   A `401 Unauthorized` response clears the local session and notifies
    the authentication context.

### Authentication Endpoints

  -------------------------------------------------------------------------
  Method            Endpoint            Access            Purpose
  ----------------- ------------------- ----------------- -----------------
  `POST`            `/api/auth/login`   Anonymous         Authenticate an
                                                          agent and issue a
                                                          JWT

  `GET`             `/api/auth/me`      Authenticated     Return the
                                                          current agent
  -------------------------------------------------------------------------

Login failures use a generic `Invalid email or password.` response to
avoid revealing whether an email exists or an account is inactive.

### Development Login

Use these credentials for local development after the database has been
seeded:

  Field      Value
  ---------- -----------------------------------------
  Email      `alex.turner@northwind-support.example`
  Password   `NorthwindDev!2026`

These credentials are for development and assessment use only. The
inactive seed agent `yuki.tanaka@northwind-support.example` should not
be able to log in.

### JWT Configuration

The API requires a signing key with at least 32 characters/bytes of
secret material. Supply it through environment variables and do not
commit it to Git.

PowerShell example for the current terminal session:

``` powershell
$newKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Jwt__SigningKey = $newKey
$env:Jwt__ExpiryMinutes = "60"
npm run api
```

Keep the same terminal session open while running the API so the
environment variables remain available. Generate your own key and do not
share it.

The `.env.example` file contains a placeholder. ASP.NET Core does not
automatically load the root `.env` file, so set the variables in the API
process environment or use a configuration mechanism that explicitly
loads them.

### Design Decisions

-   **Password hashing:** Passwords are not stored as plain text.
-   **JWT bearer authentication:** The frontend sends the token in the
    `Authorization` header for protected requests.
-   **Authentication by default:** The API uses a fallback authorization
    policy, with the login endpoint explicitly marked anonymous.
-   **Generic login errors:** Failed attempts return the same message
    for invalid credentials and inactive/missing accounts.
-   **Session storage:** `sessionStorage` is a simple assessment
    implementation and is not immune to XSS.
-   **Server-side authorization:** Hiding frontend routes is not a
    substitute for API authorization.

### Authentication Testing

Integration tests cover invalid password, non-existent email, inactive
agent, successful login and token response, access to `/api/auth/me`
without a token, and access with a valid token.

### Related Commit

-   `0c9ae56` --- `feat: add JWT authentication and protected routes`

------------------------------------------------------------------------

## Task 4: Dockerise the Application and API

**Status: Completed and locally verified**

### Docker services

The root `docker-compose.yml` defines three services:

| Service | Purpose | Host address |
| --- | --- | --- |
| `web` | Builds the React frontend and serves it with Nginx | `http://localhost:3000` |
| `api` | Runs the .NET 10 Web API | `http://localhost:8080` |
| `sqlserver` | Runs SQL Server 2022 Developer edition for local development | `localhost,1433` |

The web container serves the frontend and proxies `/api/` requests to
the API container at `http://api:8080`. The frontend uses
`VITE_API_BASE_URL=/api`.

The API uses the Compose service name `sqlserver` in its connection
string and waits for the SQL Server health check before starting.
In the Development environment, the application runs its existing
Entity Framework Core migration and seeding routine at startup. During
verification, the API reported that the database was up to date and
that existing seed data was present.

### Docker files

- `apps/api/Dockerfile` — multi-stage .NET 10 build and runtime image.
- `apps/web/Dockerfile` — multi-stage Node/Vite build and Nginx runtime image.
- `apps/web/nginx.conf` — serves the frontend, proxies API requests, and supports frontend routes.
- `.dockerignore` — excludes local secrets, Git data, and build artifacts from the build context.
- `docker-compose.yml` — configures the web, API, and SQL Server services.

### Configure environment variables

Docker Compose reads interpolation values from the root `.env` file.
Create it from the example if needed:

```powershell
Copy-Item .env.example .env
```

Edit `.env` locally and set a newly generated `Jwt__SigningKey` with at
least 32 characters/bytes of secret material. Set `MSSQL_SA_PASSWORD`
to a suitable local development password and ensure the same password
is used for SQL Server and the API connection string. Do not use example
values in a deployed environment. Never paste secrets into source files,
commit `.env`, or bake secrets into Docker images.

The root `.env` file is excluded from the Docker build context and
should be ignored by Git. ASP.NET Core does not automatically load a
`.env` file inside the API; Compose passes the required values into the
container as environment variables.

### Build and start the complete stack

Run these commands from the repository root in PowerShell:

```powershell
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
```

Open the application at:

- Frontend: `http://localhost:3000`
- API Swagger (Development environment): `http://localhost:8080/swagger`

To inspect service logs:

```powershell
docker compose logs --tail 100 api
docker compose logs --tail 100 web
docker compose logs --tail 100 sqlserver
```

To stop the containers while preserving the database volume:

```powershell
docker compose down
```

To remove the containers and database volume, deleting the local SQL
Server data, use this only when you intentionally want a clean database:

```powershell
docker compose down -v
```

### Verification performed

The following checks succeeded locally:

- `docker compose config --quiet` completed without configuration errors.
- `docker compose build` built the API and web images successfully.
- `docker compose up -d` started the SQL Server, API, and web containers.
- `docker compose ps` showed SQL Server as healthy and the API and web containers as running.
- API logs confirmed the application started on port `8080`, migrations were up to date, and existing seed data was preserved.
- The web container was corrected to use a valid Nginx configuration, rebuilt, and recreated.
- `Invoke-WebRequest http://localhost:3000 -UseBasicParsing` returned HTTP `200 OK`.
- Manual testing through the frontend confirmed login, ticket listing, filtering, SLA information, escalation, session persistence after refresh, and logout.

These checks verify the local development workflow. They do not
constitute a production deployment or a security review.

------------------------------------------------------------------------

## Prerequisites

Install the following tools:

-   .NET 10 SDK
-   Node.js 20 or later
-   npm
-   Docker Desktop with WSL 2 on Windows
-   Git

## Quick Start: Local Development

Run the following commands from the repository root in PowerShell.

### 1. Install dependencies

``` powershell
npm install
```

### 2. Configure the local environment

Copy the example environment file if required by the repository scripts:

``` powershell
Copy-Item .env.example .env
```

Review the values before starting services. Do not commit actual
secrets.

Set the JWT variables in the same PowerShell terminal that will launch
the API:

``` powershell
$newKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Jwt__SigningKey = $newKey
$env:Jwt__ExpiryMinutes = "60"
```

### 3. Start SQL Server

``` powershell
npm run db:up
```

Wait until the SQL Server container is healthy before starting the API.

### 4. Start the API

In the terminal where the JWT environment variables are set:

``` powershell
npm run api
```

Development API URLs:

-   HTTPS: `https://localhost:7043`
-   HTTP: `http://localhost:5043`

The API migrates and seeds the database automatically when running in
the Development environment. Do not assume this development behaviour
applies to production.

### 5. Start the frontend

Open another terminal at the repository root:

``` powershell
npm run web
```

Open `http://localhost:5173/tickets`. If you are not logged in, the
protected ticket page should redirect you to the login page.

### 6. Log in

Use the development credentials listed in the Task 3 section.

### 7. Stop the services

Stop the API and frontend terminals with `Ctrl+C`. Stop SQL Server using
the repository's supported Docker script or Docker Compose command.

------------------------------------------------------------------------

## Testing

Run the full test suite from the repository root:

``` powershell
npm test
```

### Latest Recorded Results

The previously recorded full test run passed:

- Backend: **55 tests passed**
- Frontend: **3 tests passed**
- Total: **58 tests passed**

After the Docker files and configuration were added, `npm test` completed
successfully again. The displayed frontend test output showed **3 tests
passed** across two test files. `npm run build` also completed
successfully: the .NET solution build succeeded, TypeScript compilation
succeeded, and Vite produced the frontend production bundle.

Run `npm test` again after further code changes and use the latest
terminal output for final submission evidence.

### Production Build

Run the full solution build:

```powershell
npm run build
```

This runs `dotnet build` and the frontend TypeScript/Vite production
build.

------------------------------------------------------------------------

## API Endpoint Overview

The table lists endpoints added or specifically relevant to this
assessment. Other endpoints from the original application remain
available.

  Method   Endpoint                          Purpose
  -------- --------------------------------- -------------------------------------
  `POST`   `/api/auth/login`                 Log in and receive a JWT
  `GET`    `/api/auth/me`                    Get the current authenticated agent
  `POST`   `/api/tickets/{id}/escalate`      Escalate a ticket
  `GET`    `/api/tickets/{id}/escalations`   Get escalation history

Most API endpoints require authentication. A request without a valid
bearer token should receive `401 Unauthorized`, except for endpoints
explicitly configured to allow anonymous access.

------------------------------------------------------------------------

## Development Seed Data

  Agent            Active     Open tickets / limit Specialisations
  ---------------- -------- ---------------------- --------------------
  Alex Turner      Yes                      5 / 10 General, Technical
  Ben Osei         Yes                       3 / 8 Technical, Outage
  Priya Nair       Yes                       4 / 8 Outage, Security
  Sara Lindqvist   Yes                       2 / 6 General, Billing
  Marcus Doyle     Yes                6 / 6 (full) Billing, Security
  Yuki Tanaka      No                        0 / 8 Technical

The assignment's sample premium customers are Contoso, Adventure Works,
and Wide World Importers. Standard customers include Fabrikam, Northwind
Traders, and Tailspin Toys. Categories include General, Billing,
Technical, Outage, and Security.

Seed data is for local development and assessment testing only.

------------------------------------------------------------------------

## Design Principles and Assumptions

1.  **Keep filtering correct:** Apply filtering before counting and
    pagination.
2.  **Keep business rules on the backend:** Triage, assignment, SLA
    calculation, escalation validation, and authorization are enforced
    server-side.
3.  **Make rules configurable where practical:** SLA durations are not
    duplicated as frontend-only rules.
4.  **Record important changes:** Escalation history is persisted.
5.  **Do not trust client identity claims:** The backend derives the
    escalation actor from the authenticated principal.
6.  **Avoid committing secrets:** JWT signing keys are supplied through
    environment configuration.
7.  **Keep deployment claims accurate:** the Docker Compose workflow
    has been verified locally for development; production deployment
    requires separate configuration and validation.

More detailed implementation notes and decisions are documented in
`APPROACH.md`.

------------------------------------------------------------------------

## AI Tool Usage

AI tools were used as an assistance tool during implementation and
documentation. The candidate should review and understand the submitted
implementation, including root causes, business rules, authentication
flow, tests, and configuration decisions.

The candidate should be able to explain:

-   Why filtering must happen before counting and pagination.
-   Why only active agents with capacity and a matching specialisation
    can be assigned.
-   How priority determines the SLA duration.
-   How escalation history is recorded.
-   How password hashing and JWT validation work.
-   Why frontend route protection alone is insufficient.
-   How environment variables are supplied to the API.
-   Which tasks are complete and which remain unfinished.

------------------------------------------------------------------------

## Known Limitations and Security Notes

-   Development login credentials are intended only for local assessment
    use.
-   JWT signing keys must be generated and supplied locally; do not
    commit them.
-   `sessionStorage` is a simple client-side token storage choice and is
    not a replacement for broader browser security measures.
-   The Compose API service uses the Development environment, which
    enables Swagger and the existing startup migration/seeding routine.
    Production deployment requires an intentional migration,
    secret-management, and seeding strategy.
-   The Docker setup is intended for local development and assessment.
    It has not been hardened or validated for production deployment.
-   ASP.NET Core Data Protection keys are not persisted by the current
    API container configuration. This is acceptable for the local
    assessment workflow but should be configured with persistent,
    appropriately protected storage if the application is deployed.
-   Test results can become outdated after code changes. Rerun the suite
    before submission.

------------------------------------------------------------------------

## Troubleshooting

### The API fails because the JWT signing key is missing

Set `Jwt__SigningKey` and `Jwt__ExpiryMinutes` in the same PowerShell
terminal that launches the API. Ensure the signing key has at least 32
characters/bytes of secret material. Do not share the key.

### The API returns `401 Unauthorized`

-   Log in through the frontend.
-   Check that the login credentials are correct.
-   Confirm that the agent is active.
-   If the token has expired or a request returns `401`, log in again.
-   Confirm that the API is running with the expected environment
    configuration.

### The frontend redirects to `/login`

This is expected when the session is missing or invalid. Log in using
the development credentials.

### The frontend container restarts or localhost:3000 refuses connections

Check the frontend logs:

```powershell
docker compose logs --tail 100 web
```

Validate `apps/web/nginx.conf`, then rebuild and recreate the web service:

```powershell
docker compose build web
docker compose up -d --force-recreate web
docker compose ps
```

### A Docker container cannot connect to another service

Inside Compose, use the service name and container port rather than
`localhost`. The API connects to SQL Server at `sqlserver,1433`, and Nginx
forwards `/api/` requests to `api:8080`.

### SQL Server is not ready

Check Docker Desktop and the SQL Server container's health before
starting the API. If the API cannot connect, verify the local
connection-string configuration and that the database container is
running.

### A .NET build or test reports that an assembly is in use

Stop the running API process with `Ctrl+C`, then rerun the build or test
command.

### Tests fail after a change

Run `npm test` again and inspect the failing test output. Do not rely on
previously recorded test results after modifying the code.

------------------------------------------------------------------------

## Commit History

Key implementation commits include:

  Commit      Description
  ----------- -----------------------------------------------
  `e82a1d2`   Fix ticket filtering before pagination
  `d55cda8`   Apply ticket filters immediately
  `2528e4f`   Update README for Task 1
  `d09d871`   Implement ticket triage and SLA escalation
  `3040d76`   Add ticket escalation UI and SLA filtering
  `2604fb7`   Update README and add implementation approach
  `0c9ae56`   Add JWT authentication and protected routes

Dockerisation files and the latest documentation updates are currently
uncommitted; their commit identifiers will be available after committing.

Check the current Git history before submission and ensure the latest
commits are pushed to your fork.

------------------------------------------------------------------------

## Submission Checklist

- [x] Complete and locally verify the Docker Compose workflow.
- [x] Run `npm test`.
- [x] Run `npm run build`.
- [x] Verify the frontend, API, and SQL Server containers start.
- [x] Manually test login and the main ticket workflows through the Docker frontend.
- [ ] Review `README.md` and `APPROACH.md` together for consistency.
- [ ] Confirm that no real JWT signing key or other secret is committed.
- [ ] Review the final Git diff and run `git diff --check`.
- [ ] Commit and push the final changes to the fork.
- [ ] Confirm the fork URL and final Git history before submission.

## Final Summary

Tasks 1–4 have been implemented. The application includes server-side
ticket filtering, ticket triage and agent assignment, SLA calculation
and escalation history, JWT-based agent authentication, protected
frontend routes, and a Docker Compose development setup for the React
frontend, .NET API, and SQL Server.

The previously recorded full test run passed 55 backend tests and 3
frontend tests. After Dockerisation changes, `npm test` completed
successfully again (the displayed frontend results were 3 passing tests),
and `npm run build` completed successfully for both the .NET solution and
frontend. The Docker images built, all three containers started, the SQL
Server health check passed, the frontend returned HTTP 200, and the core
workflows were manually verified. This is local development verification,
not production deployment validation.
