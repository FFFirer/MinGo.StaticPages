# management-console

## MODIFIED Requirements

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
