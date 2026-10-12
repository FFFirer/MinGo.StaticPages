# resolve-api

## Purpose

Read-only resolve contract for external YARP data planes (published or by version).
## Requirements
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
The resolve response SHALL include enough information for an external data plane to fetch objects directly: provider kind, bucket or local marker, object key prefix, endpoint/base URL from the project's bound storage instance, force-path-style flag, and the instance name for observability. It SHALL NOT require the data plane to call other management APIs and SHALL NOT include storage secrets. The version `prefix` in the response SHALL remain the logical version file prefix (`projects/{pid}/versions/{vid}/files`).

#### Scenario: S3 provider payload
- **WHEN** the project is bound to an `s3` storage instance
- **THEN** resolve returns `storage.provider=s3`, that instance's bucket, version prefix, `endpoint` equal to the instance service URL, `force_path_style`, and instance identity

#### Scenario: Local provider payload
- **WHEN** the project is bound to a `local` storage instance
- **THEN** resolve returns `storage.provider=local`, the version logical prefix, and `endpoint` equal to the control-plane Local file-provider base URL for that project (not the server filesystem root)

#### Scenario: Per-project endpoints differ by instance
- **WHEN** project `a` is bound to instance `minio-a` and project `b` is bound to instance `local-dev`
- **THEN** resolve for `a` and `b` returns different `storage.endpoint` values matching each instance

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

