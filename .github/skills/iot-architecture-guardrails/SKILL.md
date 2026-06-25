---
name: iot-architecture-guardrails
description: 'Generate and review C# code for IndustrialIoTPlatform using Clean Architecture, DDD tactical patterns, CQRS, and Event Sourcing. Use when creating aggregates, entities, value objects, commands, queries, handlers, repositories, API endpoints, persistence mappings, and architecture tests.'
argument-hint: 'Describe feature, bounded context, and whether it is write-side, read-side, or both.'
---

# IoT Architecture Guardrails

## Goal
This skill defines repository-specific implementation rules so generated code remains consistent with:
- Clean Architecture
- DDD (strategic and tactical patterns)
- CQRS
- Event Sourcing (event-first write model, projection-based read model)

Use this skill for any non-trivial C# change in this repository.

## Use When
- Creating or changing domain concepts in `src/Core/Domain`
- Adding commands, queries, handlers, or interfaces in `src/Core/Application`
- Implementing persistence, messaging, projections, or external integrations in `src/Infrastructure`
- Exposing API endpoints in `src/API`
- Adding architecture/unit/integration tests in `tests`

## Layering Rules (Non-negotiable)
1. Domain is pure: no EF Core, no HTTP, no message broker SDKs, no framework annotations.
2. Application depends only on Domain abstractions and contracts it owns.
3. Infrastructure implements Application ports (repositories, event store, buses, projection writers).
4. API is composition and transport only (DTO mapping, auth, validation, endpoint orchestration).
5. Dependencies must always point inward.

## DDD Rules
1. Model behavior inside aggregates, not in service scripts.
2. Enforce invariants in aggregate methods before state transitions.
3. Prefer value objects for concept-rich primitives (device code, threshold, telemetry window).
4. Keep aggregate boundaries small; reference other aggregates by id only.
5. Domain events describe facts that already happened in past tense.

## CQRS Rules
1. Commands mutate state and return minimal metadata (id/version).
2. Queries never mutate state and should read from optimized read models.
3. Do not reuse write-side aggregates for query composition.
4. Command handlers load aggregate history/events, execute behavior, append new events atomically.
5. Query handlers can use denormalized stores and cache where appropriate.

## Event Sourcing Rules
1. Aggregate state must be rebuilt from event stream replay.
2. Every state mutation on write side should result in one or more immutable events.
3. Persist events with stream id, version, timestamp, metadata, and payload.
4. Enforce optimistic concurrency with expected version checks.
5. Build read models via projection handlers; keep projection logic idempotent.
6. Support replay safety: projections must handle duplicate or out-of-order delivery strategy explicitly.

## API Rules
1. Keep controllers/endpoints thin: map request -> command/query -> response DTO.
2. Never place business rules in controllers.
3. Use explicit request/response contracts, do not expose domain entities directly.
4. Add endpoint-level validation and authorization policies.

## Testing Gates
1. Domain tests: invariants and event emission.
2. Application tests: command/query handlers with mocked ports.
3. Architecture tests: dependency direction and forbidden references.
4. Integration tests: infrastructure wiring, event store append/read, projection updates.
5. Regression tests for critical flows (device registration, start/stop, telemetry ingestion, alarm trigger).

## Standard Workflow
1. Clarify bounded context and aggregate boundary.
2. Design events first (event names and payload).
3. Define command/query contracts.
4. Implement aggregate behavior and replay logic.
5. Implement handlers and repository/event-store ports.
6. Implement infrastructure adapters.
7. Add/adjust API endpoint.
8. Add tests before finishing.

## Done Checklist
- [ ] Code obeys inward dependency rule.
- [ ] Write model uses aggregate + event stream replay.
- [ ] Read model is query-optimized and projection-driven.
- [ ] Concurrency/versioning is explicit.
- [ ] Tests added or updated at appropriate layer.
- [ ] Naming is ubiquitous-language aligned.

## References
- [Implementation Checklist](./references/implementation-checklist.md)
- [CSharp Templates](./references/csharp-templates.md)
