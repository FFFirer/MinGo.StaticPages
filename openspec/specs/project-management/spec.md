# project-management

## Purpose

Named package projects that group versions and channel pointers.
## Requirements
### Requirement: Create project
The system SHALL allow an authenticated manager to create a project with a unique flat `name`, optional display name, description, and a required `storageInstanceId`. The storage binding is immutable after create. List and detail responses SHALL expose the bound storage instance id and name.

#### Scenario: Successful create
- **WHEN** an authenticated user posts `{"name":"docs","storageInstanceId":"<id>"}` to create a project
- **THEN** the project is persisted and returned with generated id, timestamps, and the bound storage instance

#### Scenario: Duplicate name rejected
- **WHEN** a create request uses an existing project name
- **THEN** the API returns a conflict error and does not create a second project

#### Scenario: Missing storage instance rejected
- **WHEN** a create request omits `storageInstanceId` or references a non-existent instance
- **THEN** the API returns a client error and does not create the project

#### Scenario: Binding is immutable
- **WHEN** a request tries to change `storageInstanceId` on an existing project
- **THEN** the API rejects the change and the original binding remains

### Requirement: List and get projects
The system SHALL list projects for authenticated managers and return project detail including version count, storage usage summary, current channel pointers, and bound storage instance identity.

#### Scenario: List projects
- **WHEN** an authenticated user requests the project list
- **THEN** projects are returned with id, name, display name, version count, created time, and storage instance name

#### Scenario: Get project detail
- **WHEN** an authenticated user requests project detail by id
- **THEN** the response includes channel pointers (`production`, `preview`) with current version ids when set and the bound storage instance id/name

### Requirement: Delete project
The system SHALL soft-delete a project and its versions and channel pointers after explicit confirmation semantics in the API, without immediately destroying storage objects.

#### Scenario: Delete project
- **WHEN** an authenticated user deletes a project
- **THEN** the project and its versions are marked deleted and excluded from list/resolve, and storage objects remain until optional purge

