# Northwind Support Desk

Northwind Support Desk is a support ticket management system built with .NET 10 Web API, React 19, TypeScript, and SQL Server.

This repository was provided as part of a technical assessment to evaluate defect fixing, server-side filtering, ticket triage, SLA escalation, authentication, and Dockerisation.

---

## The Assignment

| Task | Description | Status |
|---|---|---|
| Task 1 | Defect Fix and Server-Side Filtering | Completed |
| Task 2 | Ticket Triage, Assignment, and SLA Escalation | Completed |
| Task 3 | Authentication and Login | Pending |
| Task 4 | Dockerise the API | Pending |

### Guidance

- The assessment is designed to be completed within two days.
- Fork the repository and commit changes as you work.
- AI tools may be used, but you should understand and explain your implementation.
- Document assumptions and design decisions.
- Focus on the requested requirements rather than implementing unrelated features.

### Submission Requirements

The submission should include:

- An updated README explaining implemented features, design decisions, root causes, development login credentials, setup instructions, testing, AI usage, and limitations.
- An `APPROACH.md` file documenting the approach taken for each task.
- Git commits showing the implementation progress.
- The forked repository link and development login credentials, where applicable.
- An estimate of the time spent on each task.

---

## Task 1: Defect Fix and Server-Side Filtering

**Status: Completed**

### Problem

The ticket listing functionality did not correctly apply all the requested filters on the server side. The frontend also did not immediately trigger requests when some filter values changed.

This caused ticket results to be inconsistent with the selected filters.

### Root Cause

The backend ticket query did not apply the requested filters consistently before calculating the total result count and retrieving the requested page.

The frontend ticket-list hook also had an incomplete effect dependency list. As a result, changes to some filter values did not immediately trigger a new request. In addition, the search debounce was applied too broadly instead of being limited to the search input.

### Implementation

The following changes were made:

- Implemented server-side filtering for ticket status.
- Implemented server-side filtering for priority.
- Implemented filtering by category.
- Implemented filtering by customer.
- Implemented filtering by assigned agent.
- Added filtering for unassigned tickets.
- Implemented search across ticket titles, references, and customer information.
- Trimmed search input to handle leading and trailing whitespace.
- Applied filtering before calculating the total result count.
- Applied sorting and pagination to the filtered results.
- Updated the frontend ticket-list hook to request updated results when filters change.
- Applied a 300 ms debounce to search input to reduce unnecessary requests.
- Added backend and frontend regression tests.

### Design Decisions

- **Server-side filtering:** Filtering is performed by the backend database query rather than filtering only the currently displayed frontend results. This ensures that pagination and result counts reflect the complete filtered dataset.
- **Filtering before pagination:** Filters are applied before counting and retrieving the requested page so that the returned records and total count remain consistent.
- **Search handling:** Search input is trimmed to avoid unnecessary differences caused by whitespace.
- **Debounced search:** Only the search input is debounced. Other filters trigger requests immediately so that users can see the updated results without unnecessary delays.

### Testing

The backend and frontend tests were run using:

```bash
npm test
```

Latest recorded results after completing Task 2:

- Backend: 49 tests passed.
- Frontend: 3 tests passed.
- Total: 52 tests passed.

### Related Commits

- `e82a1d2` — `fix: apply ticket list filters before pagination`
- `d55cda8` — `fix: apply ticket filters immediately`
- `2528e4f` — `docs: update README for Task 1`

---

## Task 2: Ticket Triage, Assignment, and SLA Escalation

**Status: Completed**

### Overview

Task 2 introduces ticket triage, automatic priority assignment, agent assignment, SLA calculation, escalation history, and SLA-based filtering.

The backend determines ticket priority and assignment when a ticket is created. The frontend allows users to escalate tickets and review escalation history.

### Ticket Priority

Tickets are assigned a priority according to the triage rules.

- The requested priority is used as the default.
- Categories marked as forcing critical priority cause the ticket to receive Critical priority.

### SLA Rules

The default SLA windows are configured as follows:

| Priority | SLA Window |
|---|---:|
| Critical | 4 hours |
| High | 8 hours |
| Medium | 24 hours |
| Low | 72 hours |

Premium customers receive half of the standard SLA window for the selected priority. The calculated window has a minimum duration of one hour.

SLA configuration is stored in the application settings so that the values can be adjusted without changing the triage logic.

### Automatic Agent Assignment

The assignment process considers agent availability and workload.

- Only active agents are eligible for assignment.
- Categories requiring a specialist are assigned only to agents with the appropriate specialisation.
- Agents at their configured maximum open-ticket capacity are excluded.
- Among eligible agents, the agent with the fewest open tickets is preferred.
- Deterministic tie-breaking is used when agents have the same workload.
- If no eligible agent is available, the ticket remains unassigned and the triage result explains why.

### Ticket Creation

When a ticket is created, the triage service determines its priority, assignment, SLA due date, and relevant triage reasons.

The ticket creation response includes the resulting priority, assigned agent where applicable, due date, and triage information.

### SLA Evaluation

The SLA evaluator supports the following statuses:

- `NotApplicable`
- `WithinSla`
- `AtRisk`
- `Breached`
- `Met`

The evaluator uses a configurable threshold to determine when a ticket should be considered at risk.

### Ticket Escalation

The escalation functionality allows an existing ticket to be moved to a higher priority.

The escalation process:

- Requires an escalation reason between 5 and 500 characters.
- Requires an actor identifier of no more than 100 characters.
- Prevents escalation of resolved or closed tickets.
- Prevents escalation when the ticket is already at Critical priority.
- Escalates priority through the sequence Low, Medium, High, and Critical.
- Recalculates the SLA due date using the new priority and customer tier.
- Records escalation history, including the escalation reason and actor.

The implementation includes an escalation domain model, database migration, repository, and command handler.

### API Endpoints Added

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/tickets/{id}/escalate` | Escalate a ticket |
| `GET` | `/api/tickets/{id}/escalations` | Retrieve escalation history |

### Frontend Implementation

The frontend changes include:

- An escalation panel on the ticket detail page.
- A form for submitting an escalation reason.
- Client-side validation for the reason field.
- Success and error feedback after submission.
- An escalation history view.
- An SLA status filter on the ticket list.
- API client methods and TypeScript types for escalation requests and responses.

### Design Decisions

- **Separate triage service:** Priority calculation and agent assignment are handled by a dedicated service to keep the ticket creation handler focused on its main responsibility.
- **Configurable SLA values:** SLA windows and the at-risk threshold are configurable instead of being scattered throughout the code.
- **Specialist-aware assignment:** Agents without the required specialisation are excluded when a category requires specialist handling.
- **Workload-based assignment:** The eligible agent with the fewest open tickets is preferred to distribute work more evenly.
- **Escalation history:** Escalations are stored as history records rather than keeping only the latest escalation state.
- **Frontend validation:** The escalation form validates user input before sending a request, while the backend also validates the request to protect the API.
- **SLA filtering:** The SLA status filter is passed to the backend so that filtering is applied to the ticket query.

### Testing

The following test results were recorded after implementing Task 2:

- Backend: 49 tests passed.
- Frontend: 3 tests passed.
- Total: 52 tests passed.

Run the tests using:

```bash
npm test
```

### Related Commits

- `d09d871` — `feat: implement ticket triage and SLA escalation`
- `3040d76` — `feat: add ticket escalation UI and SLA filtering`

---

## Quick Start

### Prerequisites

Install the following tools before running the application:

- .NET SDK 10
- Node.js 22 or a compatible version supported by the project
- npm
- Docker Desktop with Docker Compose support
- Git

Check your installed versions:

```bash
dotnet --version
node --version
npm --version
docker --version
```

### Installation

Clone your fork and navigate to the project directory.

```bash
git clone <your-fork-repository-url>
cd northwind-support
```

Copy the environment example file if required:

```bash
cp .env.example .env
```

On Windows PowerShell, use:

```powershell
Copy-Item .env.example .env
```

Install the project dependencies:

```bash
npm install
```

Start the SQL Server database:

```bash
npm run db:up
```

Start the API in the first terminal:

```bash
npm run api
```

Start the React frontend in a second terminal:

```bash
npm run web
```

### Application URLs

| Service | URL |
|---|---|
| Web application | `http://localhost:5173` |
| Ticket listing | `http://localhost:5173/tickets` |
| API over HTTPS | `https://localhost:7043` |
| API over HTTP | `http://localhost:5043` |
| Swagger UI | `https://localhost:7043/swagger` |

In the Development environment, the API applies database migrations and seeds development data on startup.

Do not commit real credentials, connection strings containing secrets, or other sensitive configuration values.

### Running with Visual Studio

1. Start the database:

   ```bash
   npm run db:up
   ```

2. Open `SupportDesk.sln` in Visual Studio.

3. Set `SupportDesk.Presentation` as the startup project.

4. Run the API using the HTTPS profile.

5. Start the frontend separately using `npm run web` if you want to use the React application.

---

## Tests

Run the complete test suite from the repository root:

```bash
npm test
```

The test command runs the backend and frontend tests.

The backend tests cover backend application behaviour, while the frontend tests use Vitest.

The automated test suite does not require the database to be running.

### Build and Quality Checks

The repository also provides commands for building, linting, type-checking, and formatting the project.

```bash
npm run build
npm run lint
npm run typecheck
npm run format
```

Run the relevant checks before committing changes.

---

## Available Commands

| Command | Description |
|---|---|
| `npm install` | Install project dependencies |
| `npm run db:up` | Start the SQL Server database |
| `npm run db:down` | Stop the database container |
| `npm run db:reset` | Reset the development database |
| `npm run api` | Start the backend API |
| `npm run web` | Start the frontend development server |
| `npm test` | Run backend and frontend tests |
| `npm run build` | Build the project |
| `npm run lint` | Run lint checks |
| `npm run typecheck` | Run TypeScript type checks |
| `npm run format` | Format the project |
| `npm run ef` | Run the configured Entity Framework command |

For the exact arguments supported by the Entity Framework command, refer to the project scripts and configuration.

---

## API at a Glance

### Ticket Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/tickets` | Retrieve tickets with filtering, sorting, and pagination |
| `GET` | `/api/tickets/{id}` | Retrieve a ticket by ID |
| `POST` | `/api/tickets` | Create a ticket and apply triage rules |
| `PATCH` | `/api/tickets/{id}/status` | Update ticket status |
| `PATCH` | `/api/tickets/{id}/assignment` | Update ticket assignment |
| `POST` | `/api/tickets/{id}/escalate` | Escalate a ticket |
| `GET` | `/api/tickets/{id}/escalations` | Retrieve ticket escalation history |

### Supporting Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/customers` | Retrieve customers |
| `GET` | `/api/agents` | Retrieve agents |
| `GET` | `/api/categories` | Retrieve ticket categories |

### Ticket List Query Parameters

The ticket list endpoint supports the following filters and options:

| Parameter | Description |
|---|---|
| `search` | Search ticket titles, references, and customer information |
| `status` | Filter by ticket status |
| `priority` | Filter by priority |
| `categoryId` | Filter by category |
| `customerId` | Filter by customer |
| `assignedAgentId` | Filter by assigned agent |
| `unassignedOnly` | Return unassigned tickets |
| `slaStatus` | Filter by SLA status |
| `page` | Select the result page |
| `pageSize` | Set the number of records per page |
| `sortBy` | Select the sorting field |
| `sortDirection` | Select the sorting direction |

The API uses ProblemDetails responses for applicable client errors, including validation failures and not-found or conflict situations.

---

## Seed Data

The development database includes sample data for testing the application.

The seed data includes:

- 5 ticket categories.
- 6 customers, including 3 Premium customers.
- 6 agents, including an inactive agent and an agent at capacity.
- 40 tickets.

This data supports testing ticket filtering, customer tiers, agent eligibility, workload-based assignment, and SLA behaviour.

---

## Authentication and Development Login Credentials

**Status: Not implemented.**

Authentication and login are part of Task 3 and remain pending.

The seeded agents are not authentication accounts. Therefore, development login credentials have not been provided by the current implementation.

Once Task 3 is implemented, update this section with the actual development login credentials and authentication instructions.

Do not invent credentials or publish real passwords.

---

## AI Usage

AI tools were used as development assistance during the assessment.

The assistance included:

- Understanding the assessment requirements.
- Investigating the ticket-filtering defect.
- Considering possible implementation approaches.
- Assisting with implementation and code review.
- Supporting the preparation of tests and documentation.

The implementation should be reviewed and understood by the candidate. All design decisions, code changes, and test results should be explainable during the assessment review.

---

## Known Limitations

The following items remain incomplete:

- **Task 3 — Authentication:** Login functionality and API authentication enforcement have not yet been implemented.
- **Task 4 — Dockerise the API:** API Dockerisation has not yet been implemented.

Other functionality outside the current assessment scope includes:

- User registration.
- Password reset.
- Role-based access control.
- OAuth or single sign-on.
- Refresh tokens.
- Multi-factor authentication.
- Notifications.
- Continuous integration pipelines.
- Caching.
- A full visual redesign.

These items should not be considered implemented unless additional code and tests are added.

---

## Troubleshooting

| Problem | Possible Solution |
|---|---|
| API cannot connect to the database | Confirm Docker Desktop is running and start the database using `npm run db:up`. |
| Port 1433 is already in use | Check whether another SQL Server instance or container is using port 1433. |
| HTTPS certificate warning | Trust or configure the local development certificate as required by your .NET environment. |
| Tickets do not load | Confirm that the API is running, the database is healthy, and the frontend is using the correct API URL. |
| SQL Server starts slowly | Wait for the database health check to pass before starting the API. |
| Frontend dependencies are missing | Run `npm install` from the repository root. |
| Build fails because files are locked | Stop the running API process and try the build again. |

---

## Current Implementation Summary

| Feature | Status |
|---|---|
| Ticket filtering and search | Completed |
| Server-side pagination and sorting | Completed |
| Immediate filter updates | Completed |
| Ticket priority triage | Completed |
| Automatic agent assignment | Completed |
| SLA calculation | Completed |
| Ticket escalation | Completed |
| Escalation history | Completed |
| SLA status filtering | Completed |
| Authentication and login | Pending |
| API authentication enforcement | Pending |
| API Dockerisation | Pending |

---

## Final Submission Checklist

Before submitting the assessment:

- [ ] Verify all completed features.
- [ ] Run `npm test` and confirm that all tests pass.
- [ ] Run the relevant build, lint, and type-check commands.
- [ ] Review the README for accuracy.
- [ ] Create `APPROACH.md` documenting the approach and decisions for each task.
- [ ] Update the README after completing Tasks 3 and 4.
- [ ] Commit and push the final changes to your fork.
- [ ] Provide the fork URL.
- [ ] Provide development login credentials after authentication is implemented.
- [ ] Record the approximate time spent on each task.