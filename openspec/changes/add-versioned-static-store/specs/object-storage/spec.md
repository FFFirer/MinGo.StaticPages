# object-storage

## ADDED Requirements

### Requirement: Unified object storage abstraction
The system SHALL store and retrieve version files through a single `IObjectStorage` abstraction supporting local filesystem and S3-compatible providers.

#### Scenario: Local provider stores and lists files
- **WHEN** storage provider is `local` and a file is uploaded under a prefix
- **THEN** the file is readable from the configured local root and appears in list results for that prefix

#### Scenario: S3-compatible provider stores and lists files
- **WHEN** storage provider is `s3` with endpoint, credentials, bucket, and force-path-style settings
- **THEN** PutObject, GetObject, ListObjectsV2, and DeleteObject operations succeed against that endpoint

#### Scenario: Prefix delete removes all objects under prefix
- **WHEN** delete-by-prefix is invoked for `projects/{pid}/versions/{vid}/files`
- **THEN** all objects under that prefix are removed and the version package path no longer serves files

### Requirement: Storage backend configuration
The system SHALL allow administrators to configure provider type, endpoint, region, credentials, bucket, key prefix, and force-path-style via system settings, and SHALL use the active configuration for all storage operations.

#### Scenario: Switch storage backend in system settings
- **WHEN** an admin saves a valid S3-compatible configuration and uploads a version
- **THEN** new version objects are written to the configured bucket/prefix and not to the previous backend

#### Scenario: Invalid configuration is rejected
- **WHEN** an admin saves S3 settings missing endpoint or bucket
- **THEN** the system rejects the save and reports validation errors without changing the active backend

### Requirement: Object key layout for versions
The system SHALL address version files using the layout `projects/{projectId}/versions/{versionId}/files/{relativePath}` and store the original zip at `projects/{projectId}/versions/{versionId}/package.zip`.

#### Scenario: Uploaded file path mapping
- **WHEN** zip entry `assets/app.js` is processed for version `vid`
- **THEN** the object key is `projects/{projectId}/versions/{vid}/files/assets/app.js`
