# project-management

## ADDED Requirements

### Requirement: Create project
The system SHALL allow an authenticated manager to create a project with a unique flat `name`, optional display name, and description.

#### Scenario: Successful create
- **WHEN** an authenticated user posts `{"name":"docs"}` to create a project
- **THEN** the project is persisted and returned with generated id and timestamps

#### Scenario: Duplicate name rejected
- **WHEN** a create request uses an existing project name
- **THEN** the API returns a conflict error and does not create a second project

### Requirement: List and get projects
The system SHALL list projects for authenticated managers and return project detail including version count, storage usage summary, and current channel pointers.

#### Scenario: List projects
- **WHEN** an authenticated user requests the project list
- **THEN** projects are returned with id, name, display name, version count, and created time

#### Scenario: Get project detail
- **WHEN** an authenticated user requests project detail by id
- **THEN** the response includes channel pointers (`production`, `preview`) with current version ids when set

### Requirement: Delete project
The system SHALL soft-delete a project and its versions and channel pointers after explicit confirmation semantics in the API, without immediately destroying storage objects.

#### Scenario: Delete project
- **WHEN** an authenticated user deletes a project
- **THEN** the project and its versions are marked deleted and excluded from list/resolve, and storage objects remain until optional purge
