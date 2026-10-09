# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents, and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**. Full requirements are in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md).

| # | Task | Done looks like | Status | Effort |
|---|---|---|---|---|
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with regression tests; list filters applied in the database with a correct count | Completed | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | Pending | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | Pending | ~4 h |
| 4 | **Dockerise the API** | `docker compose up` gives a migrated, seeded API with no baked-in secret | Pending | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering separately from each other and from the feature work.
- Where the specification is ambiguous, make a decision, document it in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA, notifications, CI, caching, and visual redesign. Mention them under *Known limitations*.

### Submission

1. Update this README with what you built, your design decisions (chosen, rejected, and why), the defect's root cause, development login credentials, how to run and test the application, AI usage, and known limitations.
2. Add an `APPROACH.md` explaining your approach and thought process for each task.
3. Commit both files, then submit the link to your fork, the development login credentials, and a rough note of time spent on each task. If the fork is private, add the reviewers as collaborators.

---

## Task 1: Defect Fix and Server-Side Filtering

**Status: Completed**

### Root cause

The ticket-list API accepted filter parameters, but the backend query did not apply the requested filters to the database query. This allowed the API to return tickets that did not match the selected filters and could produce an incorrect total count.

The frontend hook also had an incomplete effect dependency list. Changes to filters other than search, page, and page size did not reliably trigger a new API request. In addition, the same debounce delay was applied to search and other filter changes.

### Implementation

- Applied supported filters to the backend query before counting and pagination.
- Implemented filtering by status, priority, category, customer, assigned agent, and unassigned status.
- Implemented search across ticket titles, references, and customer names.
- Trimmed surrounding whitespace from search input.
- Calculated the total count after filtering and before pagination.
- Preserved sorting and pagination for filtered results.
- Updated the frontend hook so non-search filter changes trigger requests immediately.
- Kept a 300 ms debounce for search input.
- Added backend regression tests for status, priority, combined filters, customer-name search, unassigned tickets, and filtered pagination.
- Added a frontend regression test for immediate status filtering and retained the search-debounce test.

### Design decisions

- **Server-side filtering:** Applied filters through Entity Framework Core query expressions so filtering happens in the database instead of on an already-loaded list in the browser.
- **Correct total count:** Applied filters before calculating the total count and retrieving the requested page.
- **Search behavior:** Searched ticket titles, references, and customer names after trimming surrounding whitespace.
- **Debounce behavior:** Delayed search requests by 300 ms to reduce unnecessary requests while typing. Other filter changes trigger requests immediately.

### Regression tests

Run the full test suite from the repository root:

```bash
npm test
```

Latest recorded test results during development:

- Backend: 36 tests passed, 0 failed.
- Frontend: 3 tests passed, 0 failed.

These results reflect the test suite at that point in development. Run the tests again after the remaining tasks to verify the final submission.

### Task 1 commits

| Commit | Description |
|---|---|
| `e82a1d2` | `fix: apply ticket list filters before pagination` |
| `d55cda8` | `fix: apply ticket filters immediately` |

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; Node.js 20.19+ works), and Docker.

```bash
cp .env.example .env          # Optional; defaults are provided
npm install                   # Install web app dependencies
npm run db:up                 # Start SQL Server 2022 in Docker
npm run api                   # Terminal 1: migrate, seed, and start the API
npm run web                   # Terminal 2: start the React development server
```

### Application URLs

| What | Where |
|---|---|
| Web app | [http://localhost:5173](http://localhost:5173) |
| API | [https://localhost:7043](https://localhost:7043) |
| HTTP API | [http://localhost:5043](http://localhost:5043) |
| Swagger | [https://localhost:7043/swagger](https://localhost:7043/swagger) |

In Development, the API applies migrations and seeds demo data on startup.

**Never commit real credentials or secrets.**

### Visual Studio 2026

Run `npm run db:up`, open `SupportDesk.sln`, set `SupportDesk.Presentation` as the startup project, and press F5 using the `https` profile.

---

## Tests

```bash
npm test
```

This command runs the .NET tests followed by the web tests using Vitest.

The current test suites do not require a running database for test execution.

---

## Commands

| Command | What it does |
|---|---|
| `npm run db:up` | Start SQL Server |
| `npm run db:down` | Stop SQL Server |
| `npm run db:reset` | Reset the development database |
| `npm run api` | Run the API |
| `npm run web` | Run the React app |
| `npm run build` | Build the application |
| `npm run lint` | Run web lint checks |
| `npm run typecheck` | Run type checks |
| `npm run format` | Run formatting tools |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

---

## API at a glance

| Endpoint | Description |
|---|---|
| `GET /api/tickets` | Paginated ticket list with search, filtering, and sorting |
| `GET /api/tickets/{id}` | Retrieve a single ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` | Change ticket status |
| `PATCH /api/tickets/{id}/assignment` | Change ticket assignment |
| `GET /api/customers` | Retrieve customer reference data |
| `GET /api/agents` | Retrieve agent reference data |
| `GET /api/categories` | Retrieve category reference data |

### Ticket list query parameters

`GET /api/tickets` supports:

- `search`
- `status`
- `priority`
- `categoryId`
- `customerId`
- `assignedAgentId`
- `unassignedOnly`
- `page`
- `pageSize`
- `sortBy`
- `sortDirection`

The API uses `ProblemDetails` responses for errors, including validation errors (`400`), not-found responses (`404`), and conflicts or business-rule errors (`409`).

---

## Seed data

The development database includes:

- 5 categories
- 6 customers, including 3 Premium customers
- 6 agents, including one inactive agent and one at their ticket limit
- 40 tickets across different statuses, priorities, and SLA states

---

## Authentication and development login credentials

**Status: Not yet implemented.**

The current implementation does not provide application login credentials. Seeded demo agents should not be treated as authenticated user accounts.

After Task 3, update this section with the actual development login credentials and instructions for signing in.

---

## AI usage

AI tools were used as development assistance to understand the requirements, investigate the defect, discuss implementation options, draft code and regression tests, and prepare documentation.

AI-assisted changes were reviewed and tested during development. The candidate is responsible for understanding the submitted implementation, explaining the design decisions, and identifying its limitations.

---

## Known limitations

The following assessment tasks remain incomplete:

- **Task 2:** Automatic triage, ticket escalation and history, assignment behavior, SLA handling, and related UI changes.
- **Task 3:** Agent login, API authentication, and login UI.
- **Task 4:** API Dockerisation, including migration and seed behavior without baked-in secrets.

The following capabilities are outside the assignment scope:

- User registration
- Password reset
- Role-based authorization
- OAuth/SSO
- Refresh tokens
- Multi-factor authentication
- Notifications
- Continuous integration
- Caching
- Visual redesign

Update this section as the remaining work progresses so it accurately reflects the final implementation.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| API cannot reach the database | Run `npm run db:up`, wait for SQL Server to become healthy, and check `docker compose ps`. |
| Port 1433 is already in use | Check for another SQL Server instance or update the configured port consistently. |
| Browser warns about the certificate | Run `dotnet dev-certs https --trust`, or use the configured web-app proxy. |
| Web app shows "Unable to load tickets" | Check that the API is running and `VITE_API_PROXY_TARGET` points to the correct port. |
| SQL Server is slow on Apple Silicon | Check Docker Desktop's platform compatibility settings. |