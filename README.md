# Service Desk API

A ticketing REST API built with **ASP.NET Core (.NET 10)**, **Entity Framework Core** and **SQL Server**.
Customers raise support tickets, agents work them through a defined workflow, and every ticket carries an SLA due date based on its priority.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![EF Core](https://img.shields.io/badge/EF%20Core-10-512BD4) ![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927) ![Tests](https://img.shields.io/badge/tests-34%20passing-2ea44f)

## Features

- **JWT authentication** with role-based access: `Customer`, `Agent`, `Admin`
- **Ticket workflow** enforced in the domain model: Open → InProgress → Resolved → Closed, with reopen and hand-back
- **SLA tracking**: due dates calculated per priority (Critical 4h, High 8h, Medium 24h, Low 72h) and an overdue filter
- **Data isolation**: customers only ever see their own tickets (other tickets return 404, so IDs are not leaked)
- **Search, filters and pagination** on tickets, sorted by priority and due date
- **Dashboard stats** in one grouped SQL query (counts by status plus overdue count)
- **Consistent errors** as RFC 7807 Problem Details, with validation errors per field
- **Swagger UI** with a Bearer token button for trying every endpoint
- **Docker Compose** setup with SQL Server, EF Core migrations applied on startup, and **GitHub Actions** CI

## Architecture

```
Controllers  →  Services  →  Repositories  →  EF Core DbContext  →  SQL Server / SQLite
     │             │
  Contracts     Domain (entities + business rules)
  (DTOs)
```

| Layer | Responsibility |
|---|---|
| `Controllers/` | HTTP endpoints, authorization attributes, status codes |
| `Contracts/` | Request/response records with validation, and entity-to-DTO mapping |
| `Services/` | Use cases: access rules, orchestration, transactions |
| `Domain/` | Entities that enforce their own rules (`Ticket.ChangeStatus`, `AssignTo`, `AddComment`) |
| `Repositories/` | Data access behind interfaces, plus a Unit of Work |
| `Data/` | `DbContext`, Fluent API configurations, migrations, seeding |

### Design patterns and principles

- **Repository** and **Unit of Work**: services depend on `ITicketRepository` / `IUnitOfWork`, not on EF Core directly
- **Strategy**: each priority has an `ISlaPolicy`; `SlaCalculator` picks the right one, so new SLA rules need no `switch` changes
- **Dependency Injection** everywhere, including `TimeProvider` so time-based logic is testable
- **Rich domain model**: state changes go through methods, so an invalid workflow step is impossible to save
- **SOLID**: small interfaces, single-responsibility classes, behaviour extended through new implementations
- **Options pattern** with validation on startup (`JwtOptions`, `DatabaseOptions`)

## API

| Method | Endpoint | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Anyone | Create a customer account |
| POST | `/api/auth/login` | Anyone | Get a JWT access token |
| GET | `/api/users/me` | Signed in | Current user profile |
| GET | `/api/users/agents` | Agent, Admin | Users who can be assigned tickets |
| GET | `/api/tickets` | Signed in | Search with `status`, `priority`, `search`, `assignedToMe`, `overdueOnly`, `page`, `pageSize` |
| GET | `/api/tickets/{id}` | Owner or staff | Ticket details, comments and allowed next statuses |
| POST | `/api/tickets` | Signed in | Raise a ticket (SLA due date is set automatically) |
| PATCH | `/api/tickets/{id}/assign` | Agent, Admin | Assign to an agent |
| PATCH | `/api/tickets/{id}/status` | Owner or staff | Move through the workflow (customers can close or reopen resolved tickets) |
| POST | `/api/tickets/{id}/comments` | Owner or staff | Add a comment |
| GET | `/api/tickets/stats` | Agent, Admin | Counts by status and overdue total |
| GET | `/health` | Anyone | Health check |

## Run it

### With Docker (API + SQL Server)

```bash
docker compose up --build
```

Open **http://localhost:8080/swagger**. Migrations run and demo data is seeded automatically.

### Locally with the .NET SDK (no database server needed)

```bash
dotnet run --project src/ServiceDesk.Api
```

This uses SQLite by default (`appsettings.json`), so it works without installing SQL Server.

### Demo accounts

| Role | Email | Password |
|---|---|---|
| Admin | admin@servicedesk.dev | Admin@12345 |
| Agent | agent@servicedesk.dev | Agent@12345 |
| Customer | customer@servicedesk.dev | Customer@12345 |

In Swagger: call `POST /api/auth/login`, copy `accessToken`, click **Authorize** and paste it.

## Tests

```bash
dotnet test
```

**34 tests**:
- **Unit tests** cover the workflow rules, SLA calculation and domain behaviour.
- **Integration tests** boot the real API with `WebApplicationFactory` against a temporary SQLite database. They cover authentication, role checks, data isolation, validation errors, the full assign → resolve → close flow, comments, stats and filters.

## Configuration

| Setting | Default | Purpose |
|---|---|---|
| `Database:Provider` | `Sqlite` | `SqlServer` uses migrations; `Sqlite` creates the schema directly |
| `ConnectionStrings:Default` | `Data Source=servicedesk.db` | Database connection |
| `Database:Seed` | `true` | Insert demo users and tickets into an empty database |
| `Jwt:SigningKey` | dev-only value | **Set from a secret in production** (minimum 32 characters) |
| `Jwt:ExpiryMinutes` | `60` | Token lifetime |

## Tech stack

C# · ASP.NET Core 10 Web API · Entity Framework Core 10 · SQL Server 2022 · SQLite · JWT Bearer · Swagger (Swashbuckle) · xUnit · WebApplicationFactory · Docker · GitHub Actions

## Possible next steps

- Refresh tokens and an admin endpoint for managing staff accounts
- Email notifications when a ticket is assigned or nears its SLA
- Background job that escalates overdue tickets
- Audit history of every status change

---

Built by [Shaik Abdulla](https://shaikabdu18.github.io/Portfolio/).
