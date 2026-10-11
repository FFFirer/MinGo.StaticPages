# release-management

## ADDED Requirements

### Requirement: Channel pointers
The system SHALL maintain one pointer per channel per project. MVP channels are `production` and `preview`. Each channel has at most one `CurrentVersionId`.

#### Scenario: Default channels exist on project create
- **WHEN** a project is created
- **THEN** channels `production` and `preview` exist with null current version until first publish

### Requirement: Publish version to channel
The system SHALL publish a version to a channel by atomically setting the channel current version and appending a release record.

#### Scenario: Successful publish
- **WHEN** an authenticated publisher publishes version `v1` to `production`
- **THEN** `production.CurrentVersionId` equals `v1`, a `ReleaseRecord` is written, and resolve-by-channel returns `v1`

#### Scenario: Publish inactive version rejected
- **WHEN** publish targets a deleted or non-existent version
- **THEN** the API rejects the request and the channel pointer is unchanged

#### Scenario: Publish across project rejected
- **WHEN** publish attempts to set a version that belongs to another project
- **THEN** the API rejects the request and the channel pointer is unchanged

### Requirement: Rollback channel
The system SHALL rollback a channel to a previous released version (or previous release record target) by updating the pointer and writing a rollback release record.

#### Scenario: Rollback to previous version
- **WHEN** an authenticated publisher rolls back `production`
- **THEN** the channel current version becomes the previous release target (or an explicitly requested prior version), and history records the rollback

### Requirement: Release history
The system SHALL record every publish and rollback with operator, channel, from-version, to-version, and timestamp.

#### Scenario: List release history
- **WHEN** an authenticated user lists release history for a project
- **THEN** records are returned newest-first including action type (`publish` or `rollback`) and version identifiers
