# management-auth

## ADDED Requirements

### Requirement: API key authentication
The system SHALL accept API keys for management API calls via `Authorization: Bearer <key>` or `X-API-Key`, and SHALL reject invalid or revoked keys.

#### Scenario: Valid API key
- **WHEN** a request includes a valid unrevoked API key on a management endpoint
- **THEN** the request is authenticated as the key's subject with that key's role claims

#### Scenario: Revoked or invalid key
- **WHEN** a request includes a missing, unknown, or revoked key
- **THEN** the API returns 401 and performs no write

### Requirement: Console cookie login
The system SHALL allow username/password login for the Blazor console and issue a cookie session for subsequent management UI API calls.

#### Scenario: Successful login
- **WHEN** a user submits valid credentials
- **THEN** a session cookie is issued and the user can access project management pages

#### Scenario: Invalid credentials
- **WHEN** a user submits invalid credentials
- **THEN** login fails without establishing a session

### Requirement: Role enforcement
The system SHALL enforce MVP roles on management operations: `admin` (full including storage settings and users/keys), `publisher` (upload, publish, rollback), `viewer` (read-only).

#### Scenario: Publisher can publish
- **WHEN** a publisher posts a publish request
- **THEN** the release is accepted if other validations pass

#### Scenario: Viewer cannot publish
- **WHEN** a viewer posts a publish or upload request
- **THEN** the API returns 403

#### Scenario: Only admin changes storage settings
- **WHEN** a non-admin updates storage settings
- **THEN** the API returns 403

### Requirement: API key management
The system SHALL allow an admin to create, list, and revoke API keys. Secret material is shown only once at creation.

#### Scenario: Create and reveal once
- **WHEN** an admin creates an API key
- **THEN** the plaintext secret is returned once and later listings show only prefix/metadata

#### Scenario: Revoke key
- **WHEN** an admin revokes an API key
- **THEN** subsequent requests using that key fail authentication
