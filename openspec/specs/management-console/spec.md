# management-console

## Purpose

Blazor Auto management console for projects, versions, releases, and storage settings.
## Requirements
### Requirement: Console navigation shell
The system SHALL provide a Blazor Web App (Auto) console with authenticated layout and navigation to projects, and storage instance management for admins.

#### Scenario: Authenticated shell
- **WHEN** a signed-in user opens the console
- **THEN** navigation exposes project list and, for admins, storage instances

### Requirement: Project and version UI flows
The system SHALL provide UI to create/list projects (create requires selecting a storage instance), open project detail (bound instance shown read-only), upload zip versions, browse version list and file manifest, publish, and rollback.

#### Scenario: Create project with storage instance
- **WHEN** a manager creates a project and must pick a storage instance
- **THEN** the UI requires instance selection before submit and the created project shows the bound instance

#### Scenario: Upload from project detail
- **WHEN** a publisher selects a zip and confirms upload
- **THEN** the UI calls the upload API, shows progress/errors, and the new version appears in the list

#### Scenario: Publish and rollback from project detail
- **WHEN** a publisher publishes a selected version or clicks rollback
- **THEN** the channel pointer display updates and release history reflects the change

### Requirement: Storage settings UI
Admins SHALL be able to list, create, edit, connectivity-test, and delete storage instances (delete blocked when projects reference the instance).

#### Scenario: Save storage instance
- **WHEN** an admin saves valid instance settings
- **THEN** the console confirms success and projects bound to that instance use the updated connection (with replace-backend caveats documented)

#### Scenario: Connectivity check
- **WHEN** an admin runs a connectivity/test action for an instance
- **THEN** the UI reports success or a readable error for that instance

#### Scenario: Delete blocked when in use
- **WHEN** an admin attempts to delete an instance referenced by a project
- **THEN** the UI reports that the instance is in use and does not delete it

### Requirement: Client-side data access
Console interactive components SHALL use the management HTTP API rather than direct database access, working under both Server and WebAssembly interactive modes of Auto rendering.

#### Scenario: Works in Auto mode
- **WHEN** the console runs under InteractiveAuto
- **THEN** list/detail/upload/publish actions function after WebAssembly takes over without API contract changes

### Requirement: Release history version display
The project detail release history table SHALL present the target version the same way as the version list's version number column: human-readable version number as a link to version detail.

#### Scenario: Target version matches version number presentation
- **WHEN** an authenticated user views project detail release history
- **THEN** the target version column shows the version number text linked to `/versions/{versionId}`, matching the version list's version number column form

#### Scenario: Target version number missing
- **WHEN** a release history record has no resolvable version number
- **THEN** the target version column falls back to a short version id text without using the hash-code style

