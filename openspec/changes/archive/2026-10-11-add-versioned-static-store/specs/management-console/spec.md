# management-console

## ADDED Requirements

### Requirement: Console navigation shell
The system SHALL provide a Blazor Web App (Auto) console with authenticated layout and navigation to projects, and storage settings for admins.

#### Scenario: Authenticated shell
- **WHEN** a signed-in user opens the console
- **THEN** navigation exposes project list and, for admins, storage settings

### Requirement: Project and version UI flows
The system SHALL provide UI to create/list projects, open project detail, upload zip versions, browse version list and file manifest, publish, and rollback.

#### Scenario: Upload from project detail
- **WHEN** a publisher selects a zip and confirms upload
- **THEN** the UI calls the upload API, shows progress/errors, and the new version appears in the list

#### Scenario: Publish and rollback from project detail
- **WHEN** a publisher publishes a selected version or clicks rollback
- **THEN** the channel pointer display updates and release history reflects the change

### Requirement: Storage settings UI
Admins SHALL be able to view and update storage provider settings and validate connectivity.

#### Scenario: Save storage settings
- **WHEN** an admin saves valid settings
- **THEN** the console confirms success and subsequent uploads use the new backend

#### Scenario: Connectivity check
- **WHEN** an admin runs a connectivity/test action
- **THEN** the UI reports success or a readable error for the configured endpoint

### Requirement: Client-side data access
Console interactive components SHALL use the management HTTP API rather than direct database access, working under both Server and WebAssembly interactive modes of Auto rendering.

#### Scenario: Works in Auto mode
- **WHEN** the console runs under InteractiveAuto
- **THEN** list/detail/upload/publish actions function after WebAssembly takes over without API contract changes
