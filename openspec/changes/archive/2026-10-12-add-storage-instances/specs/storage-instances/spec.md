# storage-instances

## ADDED Requirements

### Requirement: Storage instance registry
The system SHALL maintain a registry of storage instances, each with a unique name, a kind (`local` or `s3`), and kind-specific connection fields. The system SHALL support multiple instances per kind, including multiple `local` instances with distinct `LocalRoot` and `Prefix` values.

#### Scenario: Create S3 instance
- **WHEN** an admin creates a storage instance with kind `s3` and valid service URL, region, credentials, bucket, and prefix
- **THEN** the instance is persisted and available for project binding

#### Scenario: Create second Local instance
- **WHEN** an admin creates a second instance with kind `local` and a different `LocalRoot` and `Prefix`
- **THEN** both Local instances coexist and can be selected by different projects

#### Scenario: Invalid instance rejected
- **WHEN** an admin saves a `s3` instance missing service URL or bucket, or a `local` instance missing `LocalRoot`
- **THEN** the system rejects the save and does not persist the instance

### Requirement: Default Local instance seed
When the system starts and no storage instance exists, the system SHALL create a default Local instance (name `local`, `LocalRoot` `storage-data`, empty `Prefix`). This instance SHALL be deletable when no project references it and SHALL NOT be a privileged singleton.

#### Scenario: First boot seeds Local instance
- **WHEN** the application starts against an empty instance table
- **THEN** a Local instance named `local` exists and can be bound to new projects

#### Scenario: Seed instance is not immortal
- **WHEN** an admin deletes the seeded `local` instance and no project references it
- **THEN** the delete succeeds

### Requirement: Project binding is required and immutable
Project creation MUST require a valid `storageInstanceId`. After creation the binding MUST NOT change. All version object reads and writes for a project MUST use that project's storage instance.

#### Scenario: Create project with instance
- **WHEN** an authenticated user creates project `docs` with `storageInstanceId` of instance `minio-a`
- **THEN** the project is persisted with that binding and uploads go to `minio-a`

#### Scenario: Create without instance rejected
- **WHEN** a create project request omits `storageInstanceId` or references a missing instance
- **THEN** the API returns a client error and does not create the project

#### Scenario: Binding cannot change
- **WHEN** a request attempts to update a project's `storageInstanceId`
- **THEN** the API rejects the change and the binding remains unchanged

### Requirement: Per-instance object storage routing
The system SHALL resolve `IObjectStorage` from a storage instance configuration and SHALL use the project's bound instance for upload, download, list, delete, and prefix-delete of version objects. Object key layout under an instance remains `projects/{projectId}/versions/{versionId}/files/{relativePath}` (and `package.zip` for the package). Both `local` and `s3` kinds SHALL apply instance `Prefix` as the key root prefix.

#### Scenario: Upload routed to bound instance
- **WHEN** a version zip is uploaded to project `docs` bound to instance `local-archive`
- **THEN** objects are written under that instance's `LocalRoot` and `Prefix` and not under another instance

#### Scenario: Local Prefix is applied
- **WHEN** a Local instance has `Prefix=sites/prod` and a file `index.html` is stored for a version
- **THEN** the physical path is under `{LocalRoot}/sites/prod/projects/{pid}/versions/{vid}/files/index.html`

### Requirement: Instance delete guard
The system SHALL refuse to delete a storage instance that is referenced by any project and SHALL allow delete when no project references it.

#### Scenario: Delete blocked while referenced
- **WHEN** an admin deletes an instance that is bound to project `docs`
- **THEN** the API returns a conflict or validation error and the instance remains

#### Scenario: Delete when unreferenced
- **WHEN** an admin deletes an instance with zero project references
- **THEN** the instance is removed

### Requirement: Local FileProvider static access
For projects bound to a `local` instance, the system SHALL expose read-only static file access via a local file provider rooted at that instance's `LocalRoot` and `Prefix`, supporting published-channel and by-version paths, SPA fallback, and cache headers aligned with resolve hints. The system SHALL NOT provide this file-provider serving for `s3` instances (external data plane reads S3 directly). The resolve `storage.endpoint` for Local SHALL be the control-plane file-provider base URL and MUST NOT expose the server filesystem root path.

#### Scenario: Serve published Local project file
- **WHEN** a client requests a file path for project `docs` bound to a Local instance after a production publish
- **THEN** the response body is the stored file with an appropriate content type

#### Scenario: SPA fallback on Local
- **WHEN** a request path has no extension and the version enables SPA fallback `index.html`
- **THEN** the file provider serves `index.html` for that path

#### Scenario: S3 project has no Local file provider
- **WHEN** a client requests the Local file-provider route for a project bound to an S3 instance
- **THEN** the system returns not found or not available and does not read the S3 bucket through the control plane
