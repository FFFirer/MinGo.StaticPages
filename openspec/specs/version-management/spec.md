# version-management

## Purpose

Immutable versions created from validated zip uploads, with file manifests and soft delete.

## Requirements

### Requirement: Upload version package
The system SHALL allow an authenticated publisher to upload a zip package to a project, validate it, extract files to object storage, and create an immutable version record with a file manifest.

#### Scenario: Successful upload creates immutable version
- **WHEN** a valid zip is uploaded to project `docs`
- **THEN** a new version is created with number, storage prefix `projects/{pid}/versions/{vid}/files`, package object, file manifest, size totals, uploader, and status `Active`

#### Scenario: Path traversal entry rejected
- **WHEN** the zip contains an entry with `..` or an absolute path
- **THEN** upload fails, no version is created, and no partial objects remain after cleanup

#### Scenario: Limits exceeded rejected
- **WHEN** the zip exceeds configured max package size, max file count, or max single file size
- **THEN** upload fails with a clear validation error and no version is created

#### Scenario: Version number policy
- **WHEN** the client supplies version number `1.2.3` or omits it
- **THEN** the version uses the supplied number when unique in the project, or an auto-generated timestamp-based number when omitted; duplicates are rejected

### Requirement: List and inspect versions
The system SHALL list project versions and return version detail including file manifest with relative path, size, and content type.

#### Scenario: List versions
- **WHEN** an authenticated user lists versions for a project
- **THEN** versions are returned newest-first with id, number, status, size, file count, created time, and uploader

#### Scenario: Version file manifest
- **WHEN** an authenticated user requests version detail
- **THEN** the response includes all extracted relative paths with sizes and content types

### Requirement: Soft delete version
The system SHALL soft-delete a version unless it is the current pointer of any channel; deleted versions are excluded from resolve by version and from published resolution if unpublished.

#### Scenario: Delete inactive version
- **WHEN** an authenticated user deletes a version not referenced by any channel
- **THEN** the version status becomes `Deleted` and resolve-by-version returns not found

#### Scenario: Delete published version blocked
- **WHEN** an authenticated user deletes a version that is current for `production` or `preview`
- **THEN** the API rejects the request and explains that publish pointer must change first

### Requirement: Immutable version content
The system SHALL treat version file objects as write-once after upload completes; there is no API to mutate files inside an existing version.

#### Scenario: No in-place update
- **WHEN** any management request attempts to replace files of an existing version
- **THEN** the request is not supported (404 or method not allowed) and existing objects are unchanged
