## ADDED Requirements

### Requirement: Release history version display
The project detail release history table SHALL present the target version the same way as the version list's version number column: human-readable version number as a link to version detail.

#### Scenario: Target version matches version number presentation
- **WHEN** an authenticated user views project detail release history
- **THEN** the target version column shows the version number text linked to `/versions/{versionId}`, matching the version list's version number column form

#### Scenario: Target version number missing
- **WHEN** a release history record has no resolvable version number
- **THEN** the target version column falls back to a short version id text without using the hash-code style
