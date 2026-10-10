# Northwind Support Desk — Implementation Approach

**Technical assessment:** Verdentra Northwind Support Desk
**Repository:** https://github.com/MSshizan/northwind-support
**Stack:** .NET 10 Web API · React · TypeScript · SQL Server · Entity Framework Core · Docker Compose

## Purpose and scope

This document records the implementation approach, reasoning, trade-offs, and verification for the four assessment tasks. It complements [`README.md`](README.md), which contains setup and run instructions.

The solution extends the existing application rather than replacing its architecture. Business rules are enforced by the API; the frontend provides the user workflow; SQL Server remains the persistent store. Docker Compose provides a repeatable local environment for the frontend, API, and database.

| Task | Outcome |
|---|---|
| 1 — Defect fix and server-side filtering | Implemented |
| 2 — Triage, assignment, SLA, and escalation | Implemented |
| 3 — Authentication and protected routes | Implemented |
| 4 — Dockerise the API and application | Built and locally verified |

---

## Task 1 — Defect fix and server-side filtering

### Problem and root cause

The ticket list did not consistently reflect selected filters. The frontend ticket-list hook had an incomplete effect dependency list, so a change to some filter values did not reliably trigger a new API request. Search debouncing was also applied too broadly instead of only to free-text search.

The backend query also needed to apply all requested filters before calculating the total count and selecting the requested page. Otherwise, the page and total could represent different datasets.

### Implementation

**Backend**

- Supports filtering by status, priority, category, customer, assigned agent, and unassigned status.
- Supports free-text search across ticket title, reference, and customer information.
- Trims the search string so leading/trailing whitespace does not change the intended query.
- Composes filter predicates before count calculation and pagination.
- Applies sorting and pagination to the filtered query.

**Frontend**

- Updates the ticket-list hook dependencies so relevant filter changes trigger a fresh request.
- Applies a 300 ms debounce to free-text search only.
- Sends explicit dropdown/filter changes immediately instead of waiting for the search debounce.

### Why this approach

**Filter in the database.** Filtering only the current browser page would omit matching tickets outside that page. Keeping predicates in the EF Core query allows SQL Server to apply the conditions to the full result set.

**Filter before count and pagination.** The count, page records, and page count must all describe the same filtered set. The query order is therefore: compose filters → count matches → apply sort → apply pagination → return results.

**Debounce only text search.** Search can produce a request on each keystroke, so a short debounce reduces unnecessary requests. Dropdowns and other explicit filter changes should respond immediately.

### Verification

- Run the repository test command: `npm test`.
- In browser DevTools, change a non-search filter and confirm a new `GET /api/tickets` request is sent immediately.
- Type into search and confirm the request is delayed by approximately 300 ms.
- Combine filters and compare the response count and page contents.
- Check that moving between pages does not bring back records excluded by the active filters.

The recorded test runs passed. A separate SQL execution-plan benchmark has not been recorded, so no measured database speed-up is claimed.

---

## Task 2 — Ticket triage, assignment, SLA, and escalation

### 2.1 Ticket triage

The ticket creation flow uses a dedicated triage service to keep business decisions separate from request handling.

- The requested priority is the default.
- A category configured to force Critical priority overrides the requested priority.
- The triage result records reasons for the decision.

This separation makes the rules easier to test and change without growing the ticket creation handler into a collection of business rules.

### 2.2 Automatic agent assignment

The assignment process evaluates agent eligibility before choosing a candidate.

1. Exclude inactive agents.
2. Enforce required category specialisation where specialist handling is needed.
3. Exclude agents at their configured maximum open-ticket capacity.
4. Prefer the eligible agent with the fewest open tickets.
5. Apply a deterministic tie-break when workload is equal.
6. If no eligible agent remains, leave the ticket unassigned and explain why.

**Trade-off:** A deterministic, workload-based rule is predictable and testable. It does not attempt to optimise every real-world scheduling concern, such as shifts, current presence, fairness over time, or estimated handling effort; those would require additional requirements and data.

### 2.3 SLA calculation

Base SLA windows are configured by priority:

| Priority | Standard target |
|---|---:|
| Critical | 4 hours |
| High | 8 hours |
| Medium | 24 hours |
| Low | 72 hours |

Premium customers receive half of the standard window, subject to a one-hour minimum. SLA values and the at-risk threshold are configurable in application settings. The calculated due time is returned with ticket information.

The evaluator distinguishes:

- `NotApplicable`
- `WithinSla`
- `AtRisk`
- `Breached`
- `Met`

The evaluator uses an injectable clock so that boundary behaviour can be tested without relying on the real wall clock. The ticket list supports filtering by SLA status through the backend query.

**Assumption:** The current model treats the configured durations as elapsed time. Business hours, holidays, pause/resume rules, and time-zone-specific working calendars are not described as implemented.

### 2.4 Escalation

The API accepts an escalation reason only when it contains 5–500 characters. Resolved or closed tickets cannot be escalated, and Critical priority is the maximum.

For a valid escalation, the priority moves one level:

`Low → Medium → High → Critical`

The system recalculates the SLA due time using the new priority and customer tier, reevaluates assignment, and persists an escalation-history record. The actor identity is derived from validated authentication claims rather than trusting an identity supplied by the browser.

Endpoints:

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/tickets/{id}/escalate` | Escalate an eligible ticket |
| `GET` | `/api/tickets/{id}/escalations` | Retrieve escalation history |

The frontend includes an escalation panel, reason validation, success/error feedback, history display, and an SLA status filter.

### Why this approach

- **Business rules stay on the API:** UI validation improves usability but is not a security or consistency boundary.
- **Configuration instead of duplicated constants:** SLA windows and at-risk threshold can be adjusted without changing frontend logic.
- **Separate history records:** Previous escalation events remain available instead of being lost when the current ticket state changes.
- **Identity from claims:** The caller cannot claim to be another agent simply by sending a different actor ID in a request body.

### Verification

The recorded test run after Task 2 passed 49 backend tests and 3 frontend tests. Authentication tests added in Task 3 increased the recorded backend total to 55. Rerun `npm test` to obtain the current result after any changes.

---

## Task 3 — Agent authentication and protected routes

### Backend implementation

- Password hashes are stored on agent records; plaintext passwords are not stored.
- ASP.NET Core `IPasswordHasher<Agent>` is used to hash and verify passwords.
- `POST /api/auth/login` authenticates an agent.
- `GET /api/auth/me` returns the current authenticated agent.
- Login requires the agent to exist, be active, and provide a valid password.
- A successful login returns a signed JWT, expiry information, and basic agent details.
- JWT bearer authentication validates protected requests.
- A fallback authorization policy protects endpoints unless they are explicitly marked anonymous.
- Escalation actor identity comes from authenticated claims.

Failed login attempts return a generic `Invalid email or password.` message to avoid disclosing whether an email exists or an account is inactive.

### Frontend implementation

- Provides an agent login page.
- Maintains authentication state and protects routes.
- Stores the token in `sessionStorage`.
- Sends the token in the `Authorization: Bearer` header for protected API calls.
- Redirects unauthenticated users to login.
- Clears the session on logout or an unauthorized (`401`) response.

### Development credentials

Use only in the local assessment environment after development seed data has been created:

| Field | Value |
|---|---|
| Email | `alex.turner@northwind-support.example` |
| Password | `NorthwindDev!2026` |

These are test credentials, not production credentials. The inactive seeded agent should not be able to log in.

### JWT configuration and security

The API requires a signing key meeting its minimum key-length validation. Supply the key through local environment configuration; do not commit it, place it in a Dockerfile, or share it in screenshots. If a key is exposed, replace it.

`sessionStorage` is a simple assessment choice and is not immune to XSS. A production implementation should review token lifecycle and storage, Content Security Policy, HTTPS, login rate limiting, key rotation, and broader account security requirements.

### Why this approach

- **Password hashing:** Limits the impact of a database leak compared with storing plain-text passwords.
- **JWT bearer authentication:** Provides a stateless token for API requests.
- **Protected by default:** A fallback authorization policy reduces the risk of forgetting to protect a newly added endpoint.
- **Generic login failures:** Avoids exposing account existence through different error messages.
- **API authorization remains authoritative:** Hiding frontend routes alone would not stop direct API requests.

### Verification

The latest recorded full test run passed 55 backend tests and 3 frontend tests. Authentication integration tests cover invalid password, unknown email, inactive agent, successful login/token response, `/api/auth/me` without a token, and `/api/auth/me` with a valid token.

---

## Task 4 — Dockerisation and runtime verification

### Docker design

The root `docker-compose.yml` coordinates three services:

| Service | Purpose | Host access |
|---|---|---|
| `web` | React application built and served by Nginx | `http://localhost:3000` |
| `api` | .NET 10 Web API, listening internally on 8080 | `http://localhost:5080` |
| `sqlserver` | SQL Server 2022 Developer for local assessment | `localhost:1433` |

The browser loads the frontend from Nginx. Nginx forwards `/api/` requests to `http://api:8080` over the Compose network. The API connects to SQL Server using the internal hostname `sqlserver`, not `localhost`.

Relevant files:

- `apps/api/Dockerfile` — multi-stage .NET build and runtime.
- `apps/web/Dockerfile` — frontend build and Nginx runtime.
- `apps/web/nginx.conf` — frontend routing and API proxy.
- `.dockerignore` — excludes local files and build artefacts from the Docker build context.
- `docker-compose.yml` — services, network, environment, ports, and database health dependency.

### Restore caching

The API Dockerfile copies the solution, `global.json`, central MSBuild/package configuration, and API project files before running `dotnet restore`. Application source is copied afterward, and publish uses `--no-restore`.

This allows Docker to reuse the NuGet restore layer when only source files change. Changes to project files or central package configuration correctly invalidate that layer.

### Runtime user and port

The final API image uses the ASP.NET runtime image rather than carrying the full SDK into the runtime stage. The API listens on internal port `8080`, and the container runs as the non-root application user. Compose publishes host port `5080` to container port `8080`.

### Database readiness, migration and seed data

The API service depends on the SQL Server health check so it does not rely only on the database container having started. SQL Server may take additional time to become ready.

In the Development environment, the API runs the existing Entity Framework Core migration and seed routine at startup. The recorded startup logs reported that migrations were up to date and seed data was present. This is convenient for local assessment, but is not a recommended production migration policy.

### Secrets and configuration

Compose passes configuration to containers as environment variables. The local `.env` file can provide values such as the SQL Server password, connection string and JWT signing key. Keep `.env` untracked. The Dockerfiles should not copy it into an image or bake signing keys into build layers. ASP.NET Core does not automatically load a root `.env` file; Compose reads its interpolation values and passes the configured variables into the container.

For production, use a secret manager, separate environment-specific credentials, rotation, and a controlled database migration process.

### Health endpoint

`GET /health` is anonymous and includes a database health check. A successful response is useful for container health and local verification. It should not expose connection strings, signing keys, or other sensitive settings.

### Commands used for verification

Run from the repository root in PowerShell:

```powershell
# Validate Compose syntax
docker compose config --quiet

# Build the API and frontend images
docker compose build

# Start services
docker compose up -d

# Inspect status and health
docker compose ps

# Follow service logs
docker compose logs --tail 100 api
docker compose logs --tail 100 web
docker compose logs --tail 100 sqlserver

# Verify non-root API container user
docker compose exec api whoami

# Check the API health endpoint
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing
```

Stop services while preserving the database volume:

```powershell
docker compose down
```

To delete local database data and recreate it from scratch, only when that is intended:

```powershell
docker compose down -v
docker compose up --build -d
```

### Recorded local results

| Check | Recorded result |
|---|---|
| Compose configuration | `docker compose config --quiet` succeeded |
| Image build | API and web images built |
| SQL Server | Reported healthy |
| API startup | Started on container port 8080; migration/seed logs were normal |
| Frontend | `http://localhost:3000` returned HTTP 200 |
| API health | `http://localhost:5080/health` returned HTTP 200 Healthy after startup |
| Authorization | Unauthenticated `GET /api/tickets` returned HTTP 401 |
| Container user | `docker compose exec api whoami` returned `app` |
| Restore caching | Rebuild showed the NuGet restore layer as `CACHED` |
| Image size | API runtime image approximately 455 MB versus approximately 1.3 GB for the SDK image |

The image sizes are local Docker-reported values and can vary by platform and base-image version. The observed difference was approximately 845 MB, or about 65% relative to the SDK image size. It is a size comparison, not a claim of a 65% improvement in runtime performance.

The frontend login, ticket list, filtering, SLA display, escalation, refresh/session behaviour, and logout were manually checked in the Docker environment. These checks demonstrate a local development workflow; they are not a production security audit, load test, or production migration rehearsal.

---

## Testing summary

The recorded full test run after Task 3 passed:

| Suite | Tests passed |
|---|---:|
| Backend | 55 |
| Frontend | 3 |
| **Total** | **58** |

Commands used from the repository root:

```powershell
npm test
npm run build
```

The recorded build completed for the .NET solution and frontend. A later test run after Docker changes also succeeded; the displayed frontend result showed three passing tests.

**Submission note:** These are the latest recorded results available during documentation. Rerun the commands after final changes and use the current terminal output as the final evidence.

---

## Database and performance considerations

Filtering is composed in the EF Core query so predicates can be translated to SQL, and filtering is performed before count calculation and pagination. This avoids filtering only the visible browser page.

The escalation-history migration should be checked for its `TicketId` foreign key and associated index. Beyond that, document only indexes that are actually defined in the entity configuration or committed migrations. Candidate indexes for future review include commonly combined ticket filter and sort fields such as status, priority, category, assigned agent, customer and creation date.

An index should be selected from actual query patterns and execution plans; adding every candidate field as a separate index can increase storage and slow writes. No separate benchmark or execution-plan analysis is claimed in this document.

---

## Limitations and follow-up work

- The Compose setup uses the Development environment and is intended for assessment/local development, not production.
- Development login credentials must not be used in a deployed environment.
- `sessionStorage` is vulnerable to token exposure if an XSS flaw exists.
- ASP.NET Core Data Protection keys were reported as not persisted by the current container configuration. If Data Protection features require continuity, configure a persistent, appropriately protected key store.
- The current SLA model uses elapsed durations; business-hour calendars, holidays and pause/resume behaviour are not described as implemented.
- Production deployment should include a managed secret store, HTTPS, key rotation, login abuse controls, structured monitoring, backup/restore procedures, and a reviewed migration/rollback strategy.
- No production deployment, penetration test, load test or large-scale SQL benchmark is claimed.

## AI tool usage

AI tools were used as development assistance for understanding requirements, troubleshooting, exploring implementation approaches and reviewing documentation. Suggestions were checked against the assessment criteria and the repository's behaviour; they were not treated as proof of correctness. Docker requirements were checked against actual build and runtime output, including restore-layer caching, container user, health status and authorization behaviour.

The candidate remains responsible for understanding the submitted code, validating the implementation, explaining the design choices and accurately reporting the work completed. No claim is made that every AI suggestion was accepted or that AI-generated changes received an independent security audit.

## Final submission checklist

- [ ] Run `npm test` and `npm run build` against the final working tree.
- [ ] Run `git diff --check` and review `git diff`.
- [ ] Confirm `.env`, signing keys, passwords and other secrets are not committed.
- [ ] Confirm `README.md` and this document match the current code and Compose port mapping.
- [ ] Commit and push all final changes to the assessment fork.
- [ ] Include the fork URL, local development login credentials and **actual approximate time spent on each task** in the submission email.

---

**Summary:** The implementation addresses all four tasks: ticket-filtering correction, triage and SLA escalation, JWT authentication, and a locally verified Docker Compose environment. The recorded test suite passed 55 backend and 3 frontend tests, and the documented Docker checks succeeded locally. These results describe assessment verification only, not production readiness.
