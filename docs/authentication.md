# Authentication and organization boundaries

## Development mechanism

The app has shared, deliberately labeled development personas. The API requires both `Development` and `Demo:Enabled`. `X-ManyRooms-Demo: true` selects fictional volunteer Alex Morgan; `X-ManyRooms-Demo: leader` selects fictional leader Jamie Chen. The browser remembers entry/persona for navigation and request configuration. It does not store an identity token or a trusted authorization decision.

These header values are public information. Anyone with access to this development API can select either demo persona. The server accepts no arbitrary caller-supplied user ID. Do not deploy this mechanism as real authentication. Production identity is unfinished; the API fails closed when the development mechanism is unavailable.

## Boundaries already enforced

- An organization ID in a URL is not an authorization grant. Membership is checked against PostgreSQL for every organization endpoint.
- The assignment-response use case repeats the membership check and validates assignment ownership so non-HTTP clients cannot accidentally bypass it.
- Availability endpoints resolve the current membership and permit only that owner to list, create, edit, or remove unavailable ranges. Foreign-owned IDs are hidden as 404.
- Leader endpoints read the role from the organization membership. Selecting a leader route or submitting a role field is not sufficient. Leader assignment creation also checks the role in the application use case.
- Organization-scoped queries filter by organization before looking up a resource.
- Composite foreign keys include `OrganizationId` and reject cross-organization relationships, including team membership and assignments.
- Only `Accepted` and `Declined` are valid responses. Responses close when the event begins.
- The request’s expected version and EF optimistic concurrency prevent a stale tab from overwriting another response. Updating the assignment and inserting response history occur in one transaction.
- Availability edits and deletes also require an expected version. Conflict results are calculated from current persisted data; they do not grant authority to change another volunteer’s response.
- Private availability notes are returned to their owner only. A leader’s conflict explanation uses the blocked interval without exposing its note.

These checks are covered with application tests and real PostgreSQL API/integration tests. They are a foundation, not a completed security assessment or a production authorization product.

## Production replacement

Use ASP.NET authentication with a trusted OpenID Connect provider (or ASP.NET Core Identity with a deliberate account-recovery and verification design). Validate issuer, audience, signature, lifetime, and the appropriate browser/native token flow. Map a stable provider subject to the internal User ID. Do not trust a caller-provided database user ID.

Keep OrganizationMembership as the authorization source. Add invitation, role-management, and membership-revocation workflows before exposing leader/admin actions. Decide browser session/CSRF and native token storage together with the chosen provider. Use HTTPS, restricted CORS origins, secure secret storage, rate limits, and observability before a public deployment.

SMS will need verified provider webhooks, sender-to-user mapping, organization/assignment resolution, replay/idempotency handling, and an audit trail. It must call the same response use case rather than writing database statuses directly.
