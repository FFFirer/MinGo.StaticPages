# object-storage

## Purpose

Pluggable object storage (local filesystem and S3-compatible) for immutable version packages.
## Requirements
### Requirement: Unified object storage abstraction
The system SHALL store and retrieve version files through a single `IObjectStorage` abstraction supporting local filesystem and S3-compatible providers. Concrete backends SHALL be created per storage instance configuration (not a single global backend).

#### Scenario: Local provider stores and lists files
- **WHEN** storage instance kind is `local` and a file is uploaded under a prefix
- **THEN** the file is readable from that instance's local root and appears in list results for that prefix

#### Scenario: S3-compatible provider stores and lists files
- **WHEN** storage instance kind is `s3` with endpoint, credentials, bucket, and force-path-style settings
- **THEN** PutObject, GetObject, ListObjectsV2, and DeleteObject operations succeed against that endpoint

#### Scenario: Prefix delete removes all objects under prefix
- **WHEN** delete-by-prefix is invoked for `projects/{pid}/versions/{vid}/files`
- **THEN** all objects under that prefix are removed and the version package path no longer serves files

### Requirement: Storage backend configuration
The system SHALL allow administrators to configure one or more storage instances (kind, endpoint, region, credentials, bucket, key prefix, force-path-style, and local root) and SHALL use the instance bound to a project for that project's storage operations. Invalid configurations SHALL be rejected.

#### Scenario: Switch storage backend for new projects
- **WHEN** an admin creates a valid S3 storage instance and a new project is bound to it, then a version is uploaded
- **THEN** new version objects are written to that instance's bucket/prefix and not to another instance

#### Scenario: Invalid configuration is rejected
- **WHEN** an admin saves S3 settings missing endpoint or bucket
- **THEN** the system rejects the save and reports validation errors without changing any instance

### Requirement: Object key layout for versions
The system SHALL address version files using the logical layout `projects/{projectId}/versions/{versionId}/files/{relativePath}` and store the original zip at `projects/{projectId}/versions/{versionId}/package.zip`. The instance `Prefix` (if any) SHALL be applied as the key root prefix for both `local` and `s3` kinds; version records continue to store the logical prefix without the instance prefix.

#### Scenario: Uploaded file path mapping
- **WHEN** zip entry `assets/app.js` is processed for version `vid`
- **THEN** the logical object key is `projects/{projectId}/versions/{vid}/files/assets/app.js` and the physical key includes the instance prefix when configured

#### Scenario: Local instance prefix isomorphic to S3
- **WHEN** two Local instances use the same `LocalRoot` but different `Prefix` values
- **THEN** objects for projects bound to each instance are stored under disjoint prefix directories

