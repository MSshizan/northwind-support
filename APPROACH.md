# Technical Assessment – Implementation Approach

## Overview

This document explains the approach taken to implement the technical assessment tasks for the Northwind Support Desk application.

The application uses .NET Web API for the backend, React and TypeScript for the frontend, and SQL Server for data storage.

The main focus was to fix the ticket filtering defect, implement ticket triage and agent assignment, and introduce SLA evaluation and ticket escalation while maintaining the existing application structure.

---

## Task 1: Defect Fix and Server-Side Filtering

### 1. Problem Identification

The ticket listing functionality did not consistently apply the selected filters on the backend. The frontend also did not immediately request updated results when certain filter values changed.

This affected the accuracy of the ticket list, pagination, and total result count.

### 2. Root Cause

The backend query did not consistently apply all requested filters before calculating the total number of matching tickets and retrieving the requested page.

On the frontend, the ticket-list hook had an incomplete effect dependency list. This meant that some filter changes did not trigger a new API request.

The search debounce was also applied too broadly instead of being limited to the search input.

### 3. Implementation Approach

The issue was addressed through changes to both the backend and frontend.

#### Backend

- Updated the ticket query to support filtering by status, priority, category, customer, assigned agent, and unassigned status.
- Added search support for ticket titles, references, and customer information.
- Trimmed search input to handle leading and trailing whitespace.
- Applied filters before calculating the total result count.
- Applied sorting and pagination to the filtered results.

#### Frontend

- Updated the ticket-list hook so changes to filter values trigger new API requests.
- Applied a 300 ms debounce to the search input.
- Ensured other filters update immediately without waiting for the search debounce.

### 4. Design Decisions

**Server-side filtering**

Filtering was implemented on the backend rather than filtering only the records currently displayed in the browser. This allows filtering to work correctly across the complete dataset.

**Filtering before pagination**

The filters are applied before counting and retrieving the page. This ensures that the total count and returned records represent the same filtered result set.

**Debounced search**

A short delay is applied to search input to reduce unnecessary API requests while the user is typing. Other filters update immediately to provide a more responsive experience.

### 5. Testing

Regression tests were added or updated to verify the filtering behaviour.

The complete test suite was run using:

```bash
npm test
```

The latest recorded results after completing Task 2 were:

- Backend: 49 tests passed.
- Frontend: 3 tests passed.
- Total: 52 tests passed.

---

## Task 2: Ticket Triage, Assignment, and SLA Escalation

### 1. Objective

The objective was to implement automatic ticket triage, agent assignment, SLA calculation, escalation, and escalation history.

The implementation covers both backend business logic and the frontend functionality needed to interact with these features.

### 2. Ticket Priority and Triage

The triage process determines the ticket priority based on the requested priority and category rules.

- The requested priority is used as the default.
- Categories configured to force Critical priority override the requested priority.
- The triage result includes relevant reasons explaining the decisions.

The triage logic is handled by a dedicated service so that the business rules remain separate from the ticket creation handler.

### 3. Automatic Agent Assignment

The assignment process selects an eligible agent based on availability, specialisation, and workload.

The following rules are applied:

- Only active agents are considered.
- Categories requiring specialist handling can only be assigned to agents with the appropriate specialisation.
- Agents who have reached their configured maximum open-ticket capacity are excluded.
- The eligible agent with the fewest open tickets is preferred.
- A deterministic tie-breaking approach is used when agents have the same workload.
- If no eligible agent is available, the ticket remains unassigned and the result explains why.

This approach helps distribute work while respecting agent availability and category requirements.

### 4. SLA Calculation

The SLA window depends on the ticket priority.

| Priority | SLA Window |
|---|---:|
| Critical | 4 hours |
| High | 8 hours |
| Medium | 24 hours |
| Low | 72 hours |

Premium customers receive half of the standard SLA window. A minimum window of one hour is applied.

SLA values and the at-risk threshold are configurable through application settings.

The calculated SLA due date is included in the ticket information.

### 5. SLA Evaluation

The SLA evaluator determines the current SLA status of a ticket.

The supported statuses are:

- `NotApplicable`
- `WithinSla`
- `AtRisk`
- `Breached`
- `Met`

The evaluator uses a configurable threshold to determine when a ticket is considered at risk.

An SLA status filter was also added to the ticket list so that users can filter tickets by their SLA state.

### 6. Ticket Escalation

Ticket escalation allows a ticket to move to a higher priority when additional attention is required.

The escalation functionality includes the following rules:

- An escalation reason must contain between 5 and 500 characters.
- An actor identifier is required and must not exceed 100 characters.
- Resolved and closed tickets cannot be escalated.
- A ticket already at Critical priority cannot be escalated further.
- Priority increases through Low, Medium, High, and Critical.
- The SLA due date is recalculated based on the new priority and customer tier.
- Each escalation is recorded in the escalation history.

The implementation includes the escalation domain model, database migration, repository, and command handler.

### 7. API Implementation

The following endpoints were introduced:

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/tickets/{id}/escalate` | Escalate a ticket |
| `GET` | `/api/tickets/{id}/escalations` | Retrieve escalation history |

The existing ticket creation flow was also updated to use the triage service.

### 8. Frontend Implementation

The frontend was updated to support the new functionality.

The changes include:

- An escalation panel on the ticket detail page.
- A form for submitting an escalation reason.
- Client-side validation.
- Success and error feedback.
- A view of the escalation history.
- An SLA status filter on the ticket list.
- API client methods and TypeScript types for escalation requests and responses.

### 9. Design Decisions

**Separate triage service**

Priority calculation and agent assignment are handled by a dedicated service. This keeps the ticket creation handler focused and makes the business rules easier to maintain and test.

**Configurable SLA settings**

SLA windows and the at-risk threshold are stored in configuration rather than being hardcoded throughout the application.

**Workload-based assignment**

Selecting the eligible agent with the fewest open tickets helps distribute work more evenly while respecting capacity limits.

**Escalation history**

Escalations are recorded as separate history records so previous escalation events can be reviewed rather than retaining only the latest state.

**Backend validation**

The backend validates escalation requests even though the frontend also validates user input. This prevents the API from relying solely on client-side checks.

**SLA filtering**

The SLA status is passed to the backend query so filtering is applied to the ticket dataset rather than only to the records displayed in the browser.

### 10. Testing

The complete test suite was run using:

```bash
npm test
```

The latest recorded results were:

- Backend: 49 tests passed.
- Frontend: 3 tests passed.
- Total: 52 tests passed.

These results reflect the test run recorded after implementing Task 2.

---

## Task 3: Authentication and Login

### Status: Pending

Authentication has not yet been implemented.

The planned work includes:

- Implementing agent sign-in.
- Adding the login interface.
- Protecting API endpoints against unauthenticated requests.
- Handling authentication failures appropriately.
- Adding tests for login and API authentication.

The final implementation approach and design decisions should be documented after this task is completed.

---

## Task 4: Dockerise the API

### Status: Pending

API Dockerisation has not yet been implemented.

The planned work includes:

- Creating a Dockerfile for the API.
- Configuring the API container and required environment variables.
- Ensuring the API can connect to the SQL Server database.
- Documenting how to build and run the container.
- Testing the containerised application.

The final implementation approach and any environment or configuration decisions should be documented after this task is completed.

---

## Challenges and Considerations

The main considerations during implementation were:

- Ensuring that backend filtering, result counts, sorting, and pagination remain consistent.
- Triggering frontend requests correctly when filter values change.
- Keeping ticket triage and assignment rules separate from the ticket creation handler.
- Respecting agent availability, specialisation, and workload during assignment.
- Keeping SLA configuration adjustable.
- Recording escalation history and recalculating the SLA due date after escalation.
- Validating user input on both the frontend and backend.
- Maintaining compatibility with the existing project structure.

---

## Final Summary

Task 1 and Task 2 have been implemented.

Task 1 addresses the ticket filtering defect and frontend filter behaviour.

Task 2 introduces ticket triage, automatic agent assignment, SLA calculation, escalation history, and SLA-based filtering.

The latest recorded test run passed 49 backend tests and 3 frontend tests.

Task 3 (Authentication and Login) and Task 4 (Dockerise the API) remain pending and should be documented after implementation.