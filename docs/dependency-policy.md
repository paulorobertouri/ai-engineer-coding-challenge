# Dependency License Policy

This project must remain runnable and maintainable without paid tooling or required cloud accounts.

## Allowed dependency approach

Prefer this order when adding functionality:

1. Built-in .NET or ASP.NET Core features.
2. Built-in React, TypeScript, Vite, Playwright, or browser platform features.
3. Free and open-source packages with clearly acceptable licenses.

## Acceptable licenses

Dependencies should use permissive licenses suitable for commercial and internal use, such as:

- MIT
- Apache-2.0
- BSD-2-Clause
- BSD-3-Clause
- ISC
- MPL-2.0 only when its file-level obligations are understood and acceptable

## Disallowed or high-risk licenses

Do not add dependencies that require paid commercial licenses or use strong copyleft licenses that would create distribution or internal-use risk for this project.

Examples blocked by policy and CI:

- GPL-1.0
- GPL-2.0
- GPL-3.0
- AGPL-1.0
- AGPL-3.0
- LGPL-2.0
- LGPL-2.1
- LGPL-3.0
- SSPL-1.0

Avoid dependencies with:

- unclear or custom commercial terms
- source-available but non-open-source licenses
- paid-only SDK requirements for the default local workflow

## Scope

This policy applies to:

- backend runtime dependencies
- frontend runtime dependencies
- test dependencies
- e2e tooling
- Docker build/runtime images and helper tooling
- CI helper tools and scripts added to the repository

## Contributor requirements for new dependencies

Before adding a dependency:

1. Confirm built-in platform features are not sufficient.
2. Record the package name, purpose, and license in the pull request description.
3. Prefer the smallest dependency that solves the problem clearly.
4. Avoid adding libraries that require paid seats, paid build plugins, or cloud-only access.
5. Prefer optional adapters over mandatory hosted-service integrations.

## Enforcement

CI enforcement already checks licenses as part of the supply-chain workflow in [.github/workflows/ci.yml](../.github/workflows/ci.yml).

Current enforcement includes:

- Node dependency license checks for frontend and e2e packages.
- .NET dependency license checks using the forbidden-license policy in [security/licenses/dotnet-forbidden-licenses.json](../security/licenses/dotnet-forbidden-licenses.json).
- supply-chain documentation and local runner in [docs/supply-chain-security.md](supply-chain-security.md) and [scripts/supply-chain-security.sh](../scripts/supply-chain-security.sh).

## Docker and base image guidance

- Prefer official upstream images with clear provenance and maintained tags.
- Keep Dockerfile additions minimal and auditable.
- Do not introduce proprietary scanners or paid container build tooling as required steps.
