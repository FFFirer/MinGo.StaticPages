# resolve-api

## ADDED Requirements

### Requirement: Resolve published channel
The system SHALL expose `GET /api/resolve/{projectName}` returning storage metadata for the project's current published version (default channel `production`).

#### Scenario: Resolve published version
- **WHEN** a client resolves project `docs` without version and the `production` channel points at version `v2`
- **THEN** the response identifies `v2`, `access=published`, storage prefix for `v2` files, spa config, and cache `seconds` with `immutable=false`

#### Scenario: Project with no publish
- **WHEN** a client resolves a project whose default channel has no current version
- **THEN** the API returns 404 or 409 with a clear not-published error

### Requirement: Resolve specific version
The system SHALL resolve a specific immutable version via `?version={id or number}` regardless of channel pointers.

#### Scenario: Resolve by version number
- **WHEN** a client resolves `docs?version=1.4.2` and that version exists and is active
- **THEN** the response identifies that version, `access=version`, its storage prefix, and cache `immutable=true` with a long max-age recommendation

#### Scenario: Resolve missing version
- **WHEN** the requested version does not exist or is deleted
- **THEN** the API returns 404

### Requirement: Resolve alternate channel
The system SHALL accept `?channel=preview` to resolve a non-default channel pointer.

#### Scenario: Resolve preview channel
- **WHEN** a client resolves `docs?channel=preview` and preview points at `v3`
- **THEN** the response reflects version `v3` and `access=published` for channel `preview`

### Requirement: Storage payload for data plane
The resolve response SHALL include enough information for an external YARP to fetch objects directly: provider kind, bucket or local marker, object key prefix, endpoint/base URL when applicable, and force-path-style flag. It SHALL NOT require the data plane to call other management APIs.

#### Scenario: S3 provider payload
- **WHEN** active storage is S3-compatible
- **THEN** resolve returns `storage.provider=s3`, bucket, prefix, endpoint, and `force_path_style` as configured

#### Scenario: Local provider payload
- **WHEN** active storage is local
- **THEN** resolve returns `storage.provider=local` and the relative prefix so data plane tooling can detect non-S3 mode

### Requirement: SPA and cache hints
The resolve response SHALL include SPA fallback settings and cache hints so the data plane can serve SPA routes and set Cache-Control consistently.

#### Scenario: SPA fallback included
- **WHEN** project/version spa fallback is enabled with fallback file `index.html`
- **THEN** resolve returns `spa.enabled=true` and `spa.fallback=index.html`

#### Scenario: Cache hints differ by access mode
- **WHEN** access mode is `version`
- **THEN** cache is recommended immutable/long-lived; when access mode is `published`, cache uses short TTL and `immutable=false`

### Requirement: Resolve authentication option
The system SHALL support optional authentication for resolve endpoints via configuration. Default MAY be unauthenticated for trusted networks.

#### Scenario: Optional auth enforced
- **WHEN** resolve auth is required and the request has no valid credential
- **THEN** the API returns 401

#### Scenario: Optional auth disabled
- **WHEN** resolve auth is not required
- **THEN** a valid resolve request succeeds without credentials
