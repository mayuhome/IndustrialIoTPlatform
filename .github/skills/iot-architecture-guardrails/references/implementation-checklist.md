# Implementation Checklist

## 1) Model Design
- Identify bounded context and aggregate root.
- Define invariants and forbidden transitions.
- List domain events in past tense.
- Decide event versioning strategy for schema evolution.

## 2) Write Side (Command)
- Add command contract in Application.
- Add command handler in Application.
- Load aggregate event stream by aggregate id.
- Rehydrate aggregate from historical events.
- Execute domain method and capture newly raised events.
- Append events with expected version check.

## 3) Read Side (Query)
- Add query contract and handler in Application.
- Define projection document/table optimized for use-case.
- Implement projector/subscriber in Infrastructure.
- Ensure idempotent projection updates (upsert or version guard).

## 4) API Layer
- Add endpoint for command/query.
- Validate request shape and authorization.
- Map transport DTOs to command/query objects.
- Return stable response DTOs.

## 5) Cross-cutting
- Add telemetry/logging around command execution and event append.
- Include correlation id and causation id in event metadata.
- Add retry and dead-letter strategy for async projections.

## 6) Tests
- Domain tests for invariants and raised events.
- Application tests for handlers and port interactions.
- Integration tests for event store and projector flow.
- Architecture test for dependency direction.
