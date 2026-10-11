## MODIFIED Requirements

### Requirement: Release history
The system SHALL record every publish and rollback with operator, channel, from-version, to-version, and timestamp. Release history responses SHALL include human-readable version numbers for the target version (and source version when present) alongside version identifiers.

#### Scenario: List release history
- **WHEN** an authenticated user lists release history for a project
- **THEN** records are returned newest-first including action type (`publish` or `rollback`), version identifiers, and the target version number (and source version number when available)

#### Scenario: Target version number resolvable
- **WHEN** a release history record targets a version that still exists in the project
- **THEN** the record's target version number equals that version's `Number`

#### Scenario: Target version number unresolvable
- **WHEN** a release history record targets a version that no longer exists or cannot be resolved
- **THEN** the record still returns version identifiers, and the target version number is null or empty
