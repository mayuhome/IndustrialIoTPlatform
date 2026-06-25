# IndustrialIoTPlatform Copilot Instructions

For C# changes in this repository, always prioritize Clean Architecture + DDD + CQRS + Event Sourcing.

## Mandatory rules
- Keep dependency direction inward: API -> Application -> Domain; Infrastructure depends on Application/Domain abstractions.
- Keep domain model framework-free and behavior-rich.
- Use command handlers for writes and query handlers for reads.
- Use event-sourced write model for state transitions where applicable.
- Keep API endpoints thin and DTO-based.
- Add or update tests for each behavior change.

## Skill to load
When doing architectural or feature work, consult:
- `/.github/skills/iot-architecture-guardrails/SKILL.md`
- Its reference files under `/.github/skills/iot-architecture-guardrails/references/`

## Repository conventions
- Domain model code: `src/Core/Domain`
- Use-case orchestration: `src/Core/Application`
- Adapter implementations: `src/Infrastructure`
- HTTP entry point: `src/API`
- Tests: `tests`
