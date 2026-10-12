# resolve-api

## MODIFIED Requirements

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
