# IndustrialIoTPlatform Frontend Design Plan

## 1. Design Goals

The frontend should deliver three core domains:

1. Exhibition page:
- A 3D factory showroom as the primary canvas.
- All device machines shown in the 3D scene.
- Real-time status animation and data overlays.

2. Management page:
- Device lifecycle management.
- Device configuration and operational actions.

3. Data analytics page:
- Production KPI visualization.
- Trend, anomaly, and throughput analysis.

The project will use Angular 21 and integrate real-time updates through SignalR to align with backend architecture and best practices.

## 2. Information Architecture

Proposed top-level routes:

- /login
- /register
- /showroom
- /management/devices
- /analytics/production
- /analytics/device/:id

Route guards:

- Anonymous-only guard: login/register.
- Auth guard: showroom/management/analytics.

Layout strategy:

- Auth layout: minimal shell.
- Main layout: left navigation + top status bar + content outlet.

## 3. Frontend Domain Modules

Suggested domain-oriented structure under src/web/src/app:

- core/
  - config/
  - guards/
  - interceptors/
  - layout/
  - auth/
- shared/
  - ui/
  - pipes/
  - utils/
  - types/
- features/showroom/
  - scene/
  - overlays/
  - realtime/
  - components/
- features/management/
  - devices/
  - forms/
- features/analytics/
  - dashboards/
  - charts/

This keeps 3D, management, and analytics isolated and evolvable.

## 4. 3D Showroom Design

### 4.1 Technical Stack

Recommended:

- Rendering: three.js
- Angular integration: custom Angular service + rendering component (do not let every component touch raw three.js objects)
- Post-processing: bloom/outline for alarm highlighting
- Camera controls: orbit-like constrained controls (operator-friendly)

Optional high-productivity alternative:

- @angular-three stack (if team is ready for declarative 3D patterns)

### 4.2 Scene Composition

Scene layers:

- Factory shell (floor, walls, lanes).
- Machine models (device nodes).
- Environment lighting.
- Data effects (pulse, heat glow, status color).

Device visual mapping:

- Running: green animation pulse
- Active/Idle: blue steady
- Maintenance: amber breathing
- Error: red blinking + outline

### 4.3 Overlay System

3D + HUD combined design:

- Floating labels anchored to machine coordinates.
- Right-side inspector panel when selecting a machine.
- Top ticker for aggregated live events.

Displayed fields:

- DeviceCode
- CurrentTemperature
- Threshold
- Sequence
- LastUpdatedUtc
- Status

## 5. Real-time Architecture (SignalR Best Practice)

### 5.1 Why SignalR

Backend is .NET and already provides SignalR capability; SignalR is the best-aligned choice:

- Native .NET integration.
- Better reconnection workflow.
- Uniform security model with existing JWT.
- Easier typed event contract management.

### 5.2 Client Pipeline

Frontend real-time flow:

1. SignalR connection service starts after login.
2. Access token attached through accessTokenFactory.
3. ReceiveSimulationData event mapped into typed model.
4. Data fan-out to signal store (single source of truth).
5. Showroom, management, analytics subscribe to derived signals.

### 5.3 Resilience Rules

- Auto reconnect with exponential backoff.
- Connection state displayed in top bar.
- Message timestamp and sequence validation.
- Duplicate suppression by DeviceId + Sequence.
- On reconnect, trigger REST snapshot sync before resuming stream display.

### 5.4 Notification Center (Simulator-Sourced)

Notification panel in top header is fed from simulator realtime stream (in-memory only):

- Connection category:
  - Stream connected
  - Stream disconnected/error
- Status category:
  - Device status transition (e.g., Running -> Maintenance)
- Alert category:
  - Temperature crossing threshold upward

Filter chips in notification panel:

- all
- unread
- connection
- status
- alert

Rules:

- Notifications are generated from signal-store event deltas, not mocked data.
- No localStorage persistence for notifications.
- Keep a bounded in-memory list (latest N events).
- Use sequence delta checks to suppress duplicate notifications.
- Priority sorting is applied before rendering: high -> medium -> low, then by latest event.
- Support clear-current-filter action in panel (`all`, `unread`, `connection`, `status`, `alert`).
- Apply alert mute window: same device + same alert key is suppressed within 2 minutes.

## 6. State Management Strategy

Use Angular Signals as the default state model.

Stores:

- authStore
- deviceStore (master + real-time map)
- showroomStore (selection, camera mode, layer visibility)
- analyticsStore (filters, period, grouped metrics)

Derived computed examples:

- runningDeviceCount
- errorDeviceCount
- temperatureAlertRate
- topHotDevices

Rule:

- Websocket/SignalR handlers update store only.
- Components do not mutate data directly.

## 7. Management Page Design

Primary capabilities:

- Device list table (filter, sort, search).
- Detail panel and action bar.
- Actions: start, stop, maintenance toggle, simulate advance.
- Batch operations for selected devices.

UX rules:

- Optimistic UI for low-latency actions with rollback on failure.
- Command result toast + operation log panel.
- Device row status color must match showroom color semantics.

## 8. Analytics Page Design

Core sections:

- Production overview cards
- Device status distribution
- Temperature trend timeline
- Error and maintenance trend
- Device efficiency ranking

Charting suggestion:

- ECharts or ngx-charts

Data model:

- Stream + periodic snapshot merge
- Time window presets (5m, 15m, 1h, 24h)

## 9. API and Contract Layer

Create typed API gateway services:

- auth-api.service
- device-api.service
- simulation-api.service
- analytics-api.service
- realtime-signalr.service

Contract rule:

- Keep DTOs and mappers in a dedicated folder.
- Keep view-model conversion out of components.

## 10. Security and Access

- JWT handled by auth interceptor.
- SignalR connection uses token-based auth.
- Role-based UI action control (hide/disable unauthorized operations).
- Auto logout and connection teardown on 401.

## 11. Performance Targets

- 60 FPS target for showroom camera interaction on mainstream laptops.
- Avoid full scene rebuild on each real-time tick.
- Update only affected machine mesh/material.
- Debounce high-frequency UI updates while preserving data integrity.

## 12. Testing Strategy

- Unit: signal stores, mappers, realtime service.
- Component: key views and action workflows.
- Integration: SignalR mock stream + route guards + auth flow.
- E2E: login -> showroom live updates -> management action -> analytics refresh.

## 13. Implementation Phases

Phase A: Foundation

- Route/layout/core guard setup.
- Auth flow and interceptor completion.
- API service scaffolding and typed contracts.

Phase B: Real-time Backbone

- SignalR client service + reconnection + state merge.
- Simulation stream integration and health indicators.

Phase C: Showroom MVP

- Basic 3D scene + machine placement + selection panel.
- Real-time status color and animation.

Phase D: Management MVP

- Device CRUD-like operational console.
- Command feedback and operation logs.

Phase E: Analytics MVP

- KPI dashboards and trend charts.
- Cross-link from showroom/management to analytics detail.

Phase F: Hardening

- Performance profiling.
- Error boundary UX.
- Test completion and release checklist.

## 14. Immediate Next Actions for Current Codebase

1. Refactor current route table from empty to full route map.
2. Build core auth store and complete interceptor token injection.
3. Add SignalR client package and implement realtime-signalr service.
4. Scaffold showroom module with 3D scene host component.
5. Replace temporary simulation API polling mindset with stream-first UI updates.

Status update:

- Completed: top-header notification center now reads live simulator events and supports category-based filters.
