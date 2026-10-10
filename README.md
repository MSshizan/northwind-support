# Northwind Support Desk

A full-stack support ticket management application built with **ASP.NET Core (.NET 10)**, **React/TypeScript**, **Entity Framework Core**, and **SQL Server**. This repository contains the completed Northwind Support Desk technical assessment, covering ticket filtering, automated triage and assignment, SLA management, escalation history, JWT authentication, and containerisation.

**Repository:** https://github.com/MSshizan/northwind-support

## Assessment overview

| Task | Implementation |
| --- | --- |
| **1 — Defect fix and filtering** | Corrected ticket-list refresh behaviour and implemented server-side filtering before pagination. |
| **2 — Triage and SLA** | Added priority rules, capacity-aware agent assignment, configurable SLA evaluation, escalation, and audit history. |
| **3 — Authentication** | Added agent login, password hashing, JWT-protected API endpoints, and protected frontend routes. |
| **4 — Docker** | Added multi-stage container builds, Compose orchestration, SQL Server readiness, database-aware health checks, and non-root API execution. |

The implementation was built and tested locally. The Docker environment is intended for **development and assessment**, not as a production-ready deployment.

## Technology and architecture

| Layer | Technologies |
| --- | --- |
| Frontend | React, TypeScript, Vite; Nginx in Docker |
| API | ASP.NET Core Web API, .NET 10 |
| Persistence | Entity Framework Core, SQL Server |
| Security | ASP.NET Core password hashing, JWT bearer authentication |
| Testing | xUnit, Vitest |
| Deployment | Docker, Docker Compose |

In Docker Compose, the **web** container serves the frontend and proxies `/api/` to the **api** container on its internal port `8080`. The API connects to the **sqlserver** service on port `1433`. The API is published to the host on port **5080**.

## Getting started — Docker Compose

**Prerequisites:** Git and Docker Desktop with Docker Compose. On Windows, use Linux containers with WSL 2 support.

```powershell
git clone https://github.com/MSshizan/northwind-support.git
cd northwind-support
Copy-Item .env.example .env
```

Edit `.env` and supply development-only values for the SQL Server password and JWT signing key. The SQL Server password must match the API connection-string configuration. Generate a strong, unique signing key; **do not commit `.env` or real secrets**.

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose ps
```

| Service | Host URL |
| --- | --- |
| Frontend | http://localhost:3000 |
| API | http://localhost:5080 |
| API health | http://localhost:5080/health |
| Swagger (Development, if enabled) | http://localhost:5080/swagger |

Allow time for SQL Server to become healthy and for the API to apply migrations and initialise development seed data. Confirm readiness with:

```powershell
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing
docker compose logs --tail 100 api
```

Stop services without deleting the database using `docker compose down`. **`docker compose down -v` also deletes the persisted local database volume.**

### Development login

| Field | Value |
| --- | --- |
| Email | `alex.turner@northwind-support.example` |
| Password | `NorthwindDev!2026` |

This seeded account is provided **only for local assessment testing**. The inactive sample agent is not permitted to log in.

## Task 1 — Defect fix and server-side filtering

**Root cause.** The ticket-list hook did not include all relevant filter values in its request-triggering effect dependencies. Search debouncing was also applied too broadly, delaying non-search filter updates. On the API side, the query needed to apply filters **before** calculating the total count and selecting a page; otherwise the result count and displayed tickets could be inconsistent.

**Changes made:**

- Fixed the frontend request dependencies so status, priority, category, customer, assignment, and other filter changes refresh the list immediately.
- Limited the **300 ms debounce** to free-text search.
- Added API-side filtering by status, priority, category, customer, assigned agent, and unassigned tickets, with text search across ticket title, reference, and customer information.
- Trimmed search input, applied filters before counting, then sorted and paginated the filtered query.
- Added or updated regression tests for filtering behaviour.

**Design decision:** Filtering belongs in the API so pagination and totals describe the **entire matching dataset**, not just the records currently loaded in the browser.

## Task 2 — Triage, assignment, SLA, and escalation

**Triage and assignment.** A dedicated triage service uses the requested priority unless a category rule requires **Critical** priority. It records the reason for its decision. Automatic assignment considers active agents, required specialisations, remaining capacity, and current open-ticket workload. The eligible agent with the fewest open tickets is preferred, with a deterministic tie-break. Tickets remain unassigned if no agent qualifies.

**SLA rules.** Default SLA targets are configurable through application settings:

| Priority | Standard target | Premium target* |
| --- | ---: | ---: |
| Critical | 4 hours | 2 hours |
| High | 8 hours | 4 hours |
| Medium | 24 hours | 12 hours |
| Low | 72 hours | 36 hours |

*Premium targets are half of the standard duration, subject to a minimum of one hour. The at-risk threshold is also configurable.*

The evaluator reports `NotApplicable`, `WithinSla`, `AtRisk`, `Breached`, or `Met`. SLA status can be used as a server-side ticket-list filter.

**Escalation.** An authenticated agent can escalate an eligible ticket by one priority level (`Low → Medium → High → Critical`). The reason must be **5–500 characters**. Resolved/closed tickets and already-Critical tickets cannot be escalated. Successful escalation recalculates the SLA due date and persists an escalation-history record. The acting agent is obtained from validated JWT claims, not from a client-supplied identity.

The ticket details UI includes an escalation form, validation feedback, and escalation history.

**Design decision:** Triage, assignment, SLA calculation, and escalation validation are implemented on the backend so their rules apply consistently to all API clients. History is persisted to make changes traceable.

## Task 3 — Agent authentication

- Agent passwords are verified using ASP.NET Core `IPasswordHasher<Agent>`; development seed data includes password hashes.
- `POST /api/auth/login` validates credentials and active status, then returns a signed JWT and agent information.
- `GET /api/auth/me` returns the authenticated agent.
- A fallback authorization policy requires authentication by default; login and `/health` explicitly allow anonymous access.
- The React application uses protected routes, attaches bearer tokens to authenticated requests, and clears the session on logout or a `401` response.
- Failed logins use a generic error message to avoid disclosing whether an account exists.

**Trade-off:** The frontend stores the JWT in `sessionStorage` for this assessment. This simplifies the login flow but does not eliminate cross-site scripting risks. Production authentication should include a broader browser-security and session-management review.

**Configuration:** Provide `Jwt__SigningKey` and related JWT settings through the API process environment or Docker Compose. The signing key must satisfy the application's minimum length requirement (at least 32 characters/bytes). Never commit or reuse exposed signing keys.

## Task 4 — Dockerisation and health checks

The root `docker-compose.yml` runs **web**, **api**, and **sqlserver** services. SQL Server has a health check, and API startup depends on database readiness. In the Development environment, the API applies its existing EF Core migrations and development seeding at startup.

**API Dockerfile:**

1. Uses a .NET 10 SDK build stage and a smaller ASP.NET runtime stage.
2. Copies `global.json`, central build/package configuration, solution metadata, and API project files **before** `dotnet restore`, allowing the restore layer to be cached when application source changes.
3. Copies source and publishes using `--no-restore`.
4. Runs the final API container as the non-root `app` user on internal port `8080`.
5. Uses runtime configuration rather than baking credentials into the image.

The `.dockerignore` excludes unnecessary build-context files, including local secrets and generated artifacts. The frontend uses a separate multi-stage Node/Vite build and Nginx runtime image.

**Health:** `GET /health` is anonymous and uses an EF Core database health check, so it reports database connectivity rather than only confirming that the web process is running.

### Recorded Docker verification

| Check | Local result |
| --- | --- |
| `docker compose config --quiet` | Passed |
| API and web image builds | Succeeded |
| Repeat API build | NuGet restore layer reused from cache |
| SQL Server | Healthy in Compose |
| API startup | Migration check completed; seed data present |
| `docker compose exec api whoami` | `app` (non-root) |
| `GET http://localhost:5080/health` | HTTP 200, healthy |
| Unauthenticated `GET /api/tickets` | HTTP 401 |
| Frontend response | HTTP 200 at `http://localhost:3000` |

The locally observed API runtime image size was approximately **455 MB**, versus approximately **1.3 GB** for the .NET SDK base image (roughly **65% smaller**). These are indicative local measurements, not a controlled like-for-like benchmark; actual sizes vary by platform and base-image version.

## Run locally without Docker

**Prerequisites:** .NET 10 SDK, Node.js 20+, npm, and an accessible SQL Server instance.

From the repository root:

```powershell
npm install
npm run db:up
```

Configure the API connection string and JWT environment variables. Start the API in a terminal that has the JWT configuration:

```powershell
$newKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Jwt__SigningKey = $newKey
$env:Jwt__ExpiryMinutes = "60"
npm run api
```

Start the frontend in another terminal:

```powershell
npm run web
```

The documented local API launch addresses are `http://localhost:5043` and `https://localhost:7043`; the Vite frontend is typically available at `http://localhost:5173`. Check the terminal output if local ports differ. **ASP.NET Core does not automatically load the root `.env` file** when the API is run directly; environment variables must reach the API process.

## Relevant API endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/auth/login` | Anonymous | Authenticate and issue JWT |
| `GET` | `/api/auth/me` | Authenticated | Current agent |
| `GET` | `/api/tickets` | Authenticated | Filtered, paginated tickets |
| `POST` | `/api/tickets/{id}/escalate` | Authenticated | Escalate ticket |
| `GET` | `/api/tickets/{id}/escalations` | Authenticated | Escalation history |
| `GET` | `/health` | Anonymous | API and database health |

See the API source or development Swagger UI for complete request/response contracts.

## Database and performance considerations

Ticket filtering is performed in the database query **before** counting and pagination. This prevents inconsistent totals and avoids filtering only the current browser page. Assignment and escalation history are persisted through the application's EF Core data-access layer.

**Indexes:** Existing database indexes should be confirmed from EF Core entity configurations and migrations before making implementation claims. For production-sized datasets, review execution plans for frequently filtered or sorted ticket columns (for example, status, priority, category, assigned agent, customer, and creation date), then add or adjust indexes based on measured query performance. This is a **performance recommendation**, not a claim that new indexes were added in this assessment.

## Tests and build

From the repository root:

```powershell
npm test
npm run build
```

The recorded full test run passed **55 backend tests** and **3 frontend tests** (**58 total**). A later test run after Docker-related changes also succeeded, and the .NET/TypeScript/Vite production build completed successfully. These are recorded local results; rerun the commands after further code changes.

Additional manual verification covered login, ticket listing and filters, SLA information, escalation, refresh/session persistence, and logout through the Docker-served frontend.

## Design trade-offs and limitations

- The Docker Compose configuration runs in **Development** mode, including development seeding and Swagger; it is not a hardened production deployment.
- `sessionStorage` token handling requires careful XSS prevention and should be revisited for production.
- ASP.NET Core Data Protection keys are not currently persisted or encrypted at rest in the API container; configure protected persistent storage if the deployed application depends on those keys.
- SLA targets use the implemented time-window rules; business hours, holidays, pause conditions, and other enterprise SLA policies would need additional requirements and tests.
- Production deployment would also require managed secrets, TLS, login abuse controls, monitoring, backups, and a reviewed migration/rollback process.
- Database index improvements should be based on the actual schema and query plans, not assumed.

## AI assistance

AI tools were used to assist with technical explanations, debugging, implementation review, and documentation. The implementation and test results were reviewed during local development. Responsibility for the submitted code, its behaviour, and the design decisions remains with the candidate.

For additional implementation discussion, see [`APPROACH.md`](APPROACH.md).
