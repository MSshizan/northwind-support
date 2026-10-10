# Technical Assessment -- Implementation Approach

## Overview

This document explains the implementation approach for the Northwind
Support Desk technical assessment.

The application uses .NET Web API for the backend, React and TypeScript
for the frontend, and SQL Server for data storage.

The work addressed the ticket-filtering defect, ticket triage and agent
assignment, SLA evaluation and escalation, agent authentication, and
Docker Compose configuration for the frontend, API, and SQL Server.
The Docker workflow was built and manually verified locally.

------------------------------------------------------------------------

## Task 1: Defect Fix and Server-Side Filtering

**Status: Completed**

### 1. Problem Identification

The ticket listing functionality did not consistently apply selected
filters. Some frontend filter changes did not immediately request
updated results.

This affected the accuracy of the ticket list, pagination, and total
result count.

### 2. Root Cause

The backend ticket query did not consistently apply all requested
filters before calculating the total number of matching tickets and
retrieving the requested page.

On the frontend, the ticket-list hook had an incomplete effect
dependency list. Some filter changes therefore did not trigger a new API
request. Search debouncing was also applied too broadly instead of being
limited to the search input.

### 3. Implementation Approach

#### Backend

-   Updated the ticket query to support filtering by status, priority,
    category, customer, assigned agent, and unassigned status.
-   Added search support for ticket titles, references, and customer
    information.
-   Trimmed search input to handle leading and trailing whitespace.
-   Applied filters before calculating the total result count.
-   Applied sorting and pagination to the filtered results.

#### Frontend

-   Updated the ticket-list hook so changes to filter values trigger new
    API requests.
-   Applied a 300 ms debounce to the search input.
-   Ensured other filters update immediately without waiting for the
    search debounce.

### 4. Design Decisions

**Server-side filtering**

Filtering is performed by the backend query rather than only on the
records currently displayed in the browser. This allows filters to work
across the full dataset.

**Filtering before pagination**

Filters are applied before counting and retrieving the page. This
ensures the total count and returned records represent the same filtered
result set.

**Debounced search**

A short delay is applied to search input to reduce unnecessary API
requests while typing. Other filters update immediately.

### 5. Testing

Regression tests were added or updated to verify filtering behaviour.

Run the test suite using:

``` bash
npm test
```

The recorded test results after Task 2 were 49 backend tests and 3
frontend tests. Additional authentication tests were added during Task
3; the latest recorded full test results are documented in Task 3.

### 6. Related Commits

-   `e82a1d2` --- `fix: apply ticket list filters before pagination`
-   `d55cda8` --- `fix: apply ticket filters immediately`
-   `2528e4f` --- `docs: update README for Task 1`

------------------------------------------------------------------------

## Task 2: Ticket Triage, Assignment, and SLA Escalation

**Status: Completed**

### 1. Objective

Implement automatic ticket triage, agent assignment, SLA calculation,
escalation, escalation history, and frontend functionality to work with
these features.

### 2. Ticket Priority and Triage

The triage process determines ticket priority using the requested
priority and category rules.

-   The requested priority is used as the default.
-   Categories configured to force Critical priority override the
    requested priority.
-   The triage result includes reasons explaining the decisions.

The triage logic is handled by a dedicated service so that the business
rules remain separate from the ticket creation handler.

### 3. Automatic Agent Assignment

The assignment process selects an eligible agent based on activity,
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

This approach helps distribute work while respecting agent availability
and category requirements.

### 4. SLA Calculation

The base SLA window depends on ticket priority.

  Priority     Base SLA
  ---------- ----------
  Critical      4 hours
  High          8 hours
  Medium       24 hours
  Low          72 hours

Premium customers receive half of the standard SLA window. A minimum
window of one hour is applied.

SLA values and the at-risk threshold are configurable through
application settings. The calculated SLA due date is included in the
ticket information.

### 5. SLA Evaluation

The SLA evaluator determines the current SLA status of a ticket.
Supported statuses are:

-   `NotApplicable`
-   `WithinSla`
-   `AtRisk`
-   `Breached`
-   `Met`

The evaluator uses a configurable threshold to determine when a ticket
is considered at risk.

An SLA status filter is available on the ticket list. The status filter
is passed to the backend query so that filtering is applied to the
ticket dataset rather than only to the records currently displayed in
the browser.

### 6. Ticket Escalation

Ticket escalation allows a ticket to move to a higher priority when
additional attention is required.

The implementation applies these rules:

-   An escalation reason must contain between 5 and 500 characters.
-   Resolved and closed tickets cannot be escalated.
-   A ticket already at Critical priority cannot be escalated further.
-   Priority increases through Low, Medium, High, and Critical.
-   The SLA due date is recalculated based on the new priority and
    customer tier.
-   Each escalation is recorded in escalation history.

Escalation history records the escalation details so earlier events can
be reviewed.

The escalation actor is derived from the authenticated user's claims
rather than trusting an actor identity supplied by the client.

### 7. API Implementation

The following endpoints support escalation:

  ---------------------------------------------------------------------------------
  Method                  Endpoint                          Purpose
  ----------------------- --------------------------------- -----------------------
  `POST`                  `/api/tickets/{id}/escalate`      Escalate a ticket

  `GET`                   `/api/tickets/{id}/escalations`   Retrieve escalation
                                                            history
  ---------------------------------------------------------------------------------

The ticket creation flow was also updated to use the triage service.

### 8. Frontend Implementation

The frontend changes include:

-   An escalation panel on the ticket detail page.
-   A form for submitting an escalation reason.
-   Client-side validation.
-   Success and error feedback.
-   A view of escalation history.
-   An SLA status filter on the ticket list.
-   API client methods and TypeScript types for escalation requests and
    responses.

### 9. Design Decisions

**Separate triage service**

Priority calculation and agent assignment are handled by a dedicated
service. This keeps the ticket creation handler focused and makes the
business rules easier to maintain and test.

**Configurable SLA settings**

SLA windows and the at-risk threshold are stored in configuration rather
than hardcoded throughout the application.

**Workload-based assignment**

Selecting the eligible agent with the fewest open tickets helps
distribute work while respecting specialisation and capacity limits.

**Escalation history**

Escalations are recorded as separate history records so previous events
can be reviewed instead of retaining only the latest state.

**Backend validation**

The backend validates escalation requests even when the frontend also
validates user input. This prevents the API from relying only on
client-side checks.

**SLA filtering**

The SLA status is passed to the backend query so filtering is applied to
the ticket dataset rather than only to the records displayed in the
browser.

### 10. Testing

Run the complete test suite using:

``` bash
npm test
```

The recorded test results after completing Task 2 were:

-   Backend: 49 tests passed.
-   Frontend: 3 tests passed.
-   Total: 52 tests passed.

These results describe the test run recorded after Task 2. Task 3 added
authentication coverage, with the latest recorded full test results
documented in that section.

### 11. Related Commits

-   `d09d871` --- `feat: implement ticket triage and SLA escalation`
-   `3040d76` --- `feat: add ticket escalation UI and SLA filtering`

------------------------------------------------------------------------

## Task 3: Authentication and Login

**Status: Completed**

### 1. Objective

Add agent sign-in, issue and validate JWTs, protect API endpoints from
unauthenticated requests, and provide a frontend login experience.

### 2. Backend Implementation

-   Added agent password-hash storage using the `PasswordHash` property.
-   Used ASP.NET Core's `IPasswordHasher<Agent>` to hash and verify
    passwords.
-   Added the login endpoint at `POST /api/auth/login`.
-   Added the current-agent endpoint at `GET /api/auth/me`.
-   Login checks that the agent exists, is active, and has a valid
    password.
-   Successful login returns a signed JWT, expiry information, and basic
    agent details.
-   Added JWT bearer authentication and a fallback authorization policy
    so endpoints require authentication unless explicitly configured
    otherwise.
-   Kept the login endpoint anonymous so users can obtain a token.
-   Derived escalation actor identity from authenticated claims instead
    of client-supplied actor data.

### 3. Frontend Implementation

-   Added a login page for agent credentials.
-   Added authentication state and protected route handling.
-   Stored the token in `sessionStorage`.
-   Added the bearer token to authenticated API requests.
-   Redirected unauthenticated users to the login page.
-   Cleared the stored session when the user logs out.
-   Cleared the local session when an API request returns
    `401 Unauthorized`.

### 4. Authentication Endpoints

  -------------------------------------------------------------------------
  Method            Endpoint            Access            Purpose
  ----------------- ------------------- ----------------- -----------------
  `POST`            `/api/auth/login`   Anonymous         Authenticate an
                                                          agent and issue a
                                                          JWT

  `GET`             `/api/auth/me`      Authenticated     Return the
                                                          current
                                                          authenticated
                                                          agent
  -------------------------------------------------------------------------

Login failures return a generic `Invalid email or password.` message so
the API does not reveal whether an email exists or an account is
inactive.

### 5. Development Credentials

The seeded development login is:

-   Email: `alex.turner@northwind-support.example`
-   Password: `NorthwindDev!2026`

These credentials are intended only for local assessment use. The
inactive seed agent `yuki.tanaka@northwind-support.example` should not
be able to log in.

### 6. JWT Configuration

The API reads the JWT signing key from configuration. The key must
contain at least 32 characters/bytes of secret material and must not be
committed to source control.

Example for a PowerShell session:

``` powershell
$newKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Jwt__SigningKey = $newKey
$env:Jwt__ExpiryMinutes = "60"
npm run api
```

Environment variables must be set in the terminal process that starts
the API. ASP.NET Core does not automatically load a root `.env` file
unless the application or tooling explicitly configures that behaviour.

The `.env.example` file uses a placeholder instead of a real signing
key.

### 7. Design Decisions

**Password hashing**

Passwords are stored as hashes rather than plain text.

**JWT bearer authentication**

The frontend sends the token in the `Authorization` header for protected
requests.

**Authentication by default**

The API uses a fallback authorization policy, and the login endpoint is
explicitly marked as anonymous.

**Generic login errors**

A generic failure message avoids disclosing whether an account exists or
is inactive.

**Session storage**

The token is stored in `sessionStorage` and cleared on logout. This is a
straightforward assessment implementation, but browser storage is not
immune to cross-site scripting risks.

**Server-side authorization**

Frontend route protection improves the user experience but does not
replace authorization checks on the API.

### 8. Testing

The latest recorded full test run after Task 3 passed:

-   Backend: 55 tests passed.
-   Frontend: 3 tests passed.
-   Total: 58 tests passed.

Authentication integration tests cover invalid password, non-existent
email, inactive agent, successful login and token response, access to
`/api/auth/me` without a token, and access with a valid token.

Rerun `npm test` after any further code changes to verify the current
result.

### 9. Related Commit

-   `0c9ae56` --- `feat: add JWT authentication and protected routes`

------------------------------------------------------------------------

## Task 4: Dockerise the Application and API

### 1. Goal

Provide a repeatable local development workflow that builds and runs the
React frontend, .NET API, and SQL Server database through Docker Compose.
The API must connect to the database, run its existing development
migration and seed routine, and keep runtime secrets outside the images.

### 2. Docker implementation

The repository now includes:

- `apps/api/Dockerfile`: multi-stage build and runtime image using .NET 10.
- `apps/web/Dockerfile`: multi-stage frontend build using Node and an Nginx runtime.
- `apps/web/nginx.conf`: serves the built frontend, proxies `/api/` requests to the API service, and supports client-side routes.
- `.dockerignore`: excludes `.env`, Git data, dependency folders, and local build artifacts from the Docker build context.
- `docker-compose.yml`: configures the SQL Server, API, and frontend services.

The Compose services are:

| Service | Responsibility | Port |
| --- | --- | --- |
| `sqlserver` | SQL Server 2022 Developer database for local use | `1433` |
| `api` | .NET 10 Web API | `8080` |
| `web` | React frontend served by Nginx | `3000` |

The API uses `ConnectionStrings__Default` and connects to SQL Server
using the Compose hostname `sqlserver`. The API service depends on the
SQL Server health check. The frontend calls `/api` on its own origin,
and Nginx forwards those requests to `http://api:8080`.

### 3. Runtime configuration and secrets

Docker Compose passes configuration to the API through environment
variables. The root `.env` file supplies values such as
`MSSQL_SA_PASSWORD` and `Jwt__SigningKey`; it must remain local and
must not be committed. The API signing key must be generated for local
use and meet the application's minimum key-length requirement.

ASP.NET Core does not automatically read the root `.env` file. Compose
interpolates its values and passes the relevant settings into the API
container. The Dockerfiles do not copy `.env` into the image.

The current Compose configuration uses the Development environment.
This enables Swagger and the application's existing startup migration
and seeding routine. It is appropriate for local assessment and should
not be treated as a production deployment configuration.

### 4. Build and run commands

Run from the repository root:

```powershell
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
```

Application addresses:

- Frontend: `http://localhost:3000`
- API Swagger: `http://localhost:8080/swagger`

Inspect logs:

```powershell
docker compose logs --tail 100 api
docker compose logs --tail 100 web
docker compose logs --tail 100 sqlserver
```

Stop the services without deleting the database volume:

```powershell
docker compose down
```

Remove the database volume only when deliberately resetting local data:

```powershell
docker compose down -v
```

### 5. Verification performed

The following local checks succeeded:

1. `docker compose config --quiet` completed without configuration errors.
2. `docker compose build` built the API and web images.
3. `docker compose up -d` started all three services.
4. `docker compose ps` showed SQL Server as healthy and the API and web containers running.
5. API logs confirmed the API started on port `8080`, migrations were up to date, and existing seed data was present.
6. The initial Nginx configuration error was corrected; the web image was rebuilt and the web container recreated.
7. `Invoke-WebRequest http://localhost:3000 -UseBasicParsing` returned HTTP `200 OK`.
8. Manual testing through the Docker frontend confirmed login, ticket listing, filtering, SLA information, escalation, session persistence after refresh, and logout.

These results verify the local development workflow. A production deployment,
a clean-state database test, and production security hardening are not
claimed as completed.

------------------------------------------------------------------------

## Challenges and Considerations

The main considerations during implementation were:

-   Ensuring that backend filtering, result counts, sorting, and
    pagination remain consistent.
-   Triggering frontend requests correctly when filter values change.
-   Keeping ticket triage and assignment rules separate from the ticket
    creation handler.
-   Respecting agent activity, specialisation, and workload during
    assignment.
-   Keeping SLA configuration adjustable.
-   Recording escalation history and recalculating the SLA due date
    after escalation.
-   Validating user input on both the frontend and backend.
-   Ensuring protected API endpoints require authentication.
-   Avoiding hardcoded JWT signing secrets.
-   Maintaining compatibility with the existing project structure.
-   Documenting the Docker Compose workflow as locally verified, while
    distinguishing it from production deployment.

------------------------------------------------------------------------

## Known Limitations

-   Task 4 is completed and locally verified through Docker Compose.
-   The Compose configuration uses the Development environment and is
    not intended as a production deployment.
-   The development credentials are for assessment use only and must not
    be reused in production.
-   The JWT signing key must be supplied through environment
    configuration and must not be committed.
-   `sessionStorage` is a simple token storage choice and does not
    eliminate browser-side security risks.
-   Production deployment would require a deliberate strategy for
    secrets, migrations, seed data, and other operational concerns.
-   Features outside the assessment scope, such as user registration,
    password reset, role-based access control, OAuth/SSO, refresh
    tokens, and multi-factor authentication, should not be considered
    implemented unless separate code and tests exist.

------------------------------------------------------------------------

## Troubleshooting

### The frontend container restarts or localhost:3000 refuses connections

Inspect the web service logs:

```powershell
docker compose logs --tail 100 web
```

Check `apps/web/nginx.conf`, then rebuild and recreate the frontend:

```powershell
docker compose build web
docker compose up -d --force-recreate web
docker compose ps
```

### Containers cannot reach each other

Use Compose service names from inside containers: the API connects to
`sqlserver,1433`, and Nginx proxies API requests to `api:8080`.


  -----------------------------------------------------------------------
  Problem                             Possible Solution
  ----------------------------------- -----------------------------------
  API cannot connect to the database  Confirm Docker Desktop is running
                                      and start the database using
                                      `npm run db:up`.

  Port 1433 is already in use         Check whether another SQL Server
                                      instance or container is using port
                                      1433.

  HTTPS certificate warning           Trust or configure the local
                                      development certificate as required
                                      by your .NET environment.

  Tickets do not load                 Confirm the API is running, the
                                      database is healthy, and the
                                      frontend uses the correct API URL.

  SQL Server starts slowly            Wait for the database health check
                                      to pass before starting the API.

  Frontend dependencies are missing   Run `npm install` from the
                                      repository root.

  API reports a missing JWT signing   Set `Jwt__SigningKey` and
  key                                 `Jwt__ExpiryMinutes` in the same
                                      terminal used to launch the API.

  API returns `401 Unauthorized`      Log in again, check the
                                      credentials, and confirm that the
                                      agent is active.

  Build fails because files are       Stop the running API process and
  locked                              try the build again.
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## Current Implementation Summary

  Feature                              Status
  ------------------------------------ -------------------
  Ticket filtering and search          Completed
  Server-side pagination and sorting   Completed
  Immediate filter updates             Completed
  Ticket priority triage               Completed
  Automatic agent assignment           Completed
  SLA calculation and evaluation       Completed
  Ticket escalation                    Completed
  Escalation history                   Completed
  SLA status filtering                 Completed
  Authentication and login             Completed
  API authentication enforcement       Completed
  Docker Compose local workflow        Completed and locally verified

------------------------------------------------------------------------

## Final Submission Checklist

- [x] Build the API and frontend Docker images.
- [x] Start the SQL Server, API, and frontend services through Compose.
- [x] Verify SQL Server health and API startup/migration logs.
- [x] Verify the frontend returns HTTP 200.
- [x] Manually test login and the main ticket workflows through the Docker frontend.
- [x] Run `npm test` after Docker changes; the displayed frontend tests passed.
- [x] Run `npm run build`; the .NET solution and frontend builds succeeded.
- [ ] Review `README.md` and `APPROACH.md` together for consistency.
- [ ] Confirm that no real JWT signing key or other secret is committed.
- [ ] Review the final Git diff and run `git diff --check`.
- [ ] Commit and push the final changes to the fork.
-   [ ] Commit and push the final changes to the fork.
-   [ ] Provide the GitHub fork URL.
-   [ ] Provide development login credentials.
-   [ ] Record the approximate time spent on each task.

------------------------------------------------------------------------

## Final Summary

Tasks 1, 2, and 3 have been implemented. Task 1 fixes the ticket
filtering defect and ensures filters are applied consistently before
counting and pagination. Task 2 adds ticket triage, automatic agent
assignment, SLA calculation and evaluation, escalation, and escalation
history. Task 3 adds JWT-based agent authentication and protected
frontend routes.

The previously recorded full test run passed 55 backend tests and 3
frontend tests. After Dockerisation changes, `npm test` completed
successfully again, with 3 frontend tests displayed as passing, and
`npm run build` succeeded for the .NET solution and frontend. Docker
images built, all three services started, SQL Server became healthy, the
frontend returned HTTP 200, and the core workflows were manually verified.
This is local development verification, not production deployment
validation.
