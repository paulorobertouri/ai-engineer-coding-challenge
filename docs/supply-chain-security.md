# Supply Chain Security Checks

This project includes local and CI-friendly supply chain checks without paid external services.

## What is covered

- Secret scanning: Trivy filesystem scanner (`secret` scanner).
- SBOM generation: Syft SPDX JSON for backend and frontend container images.
- Image vulnerability scanning: Trivy image scanner (HIGH/CRITICAL, fail on findings).
- License checks:
  - Node dependencies (`frontend` and `e2e`) via `license-checker` with a forbidden-license policy.
  - .NET dependencies via `dotnet-project-licenses` with a forbidden-license policy file.

## Run locally

From the repository root:

```bash
./scripts/supply-chain-security.sh
```

Outputs are written to `security-reports/`.

## CI integration

The workflow job `supply-chain-security` in `.github/workflows/ci.yml` runs these checks on every push and pull request.

Generated artifacts uploaded by CI:

- `security-reports/backend-image.spdx.json`
- `security-reports/frontend-image.spdx.json`
- `security-reports/dotnet-licenses.json`

## License policy

Forbidden .NET license types are configured in:

- `security/licenses/dotnet-forbidden-licenses.json`

Forbidden Node license identifiers are enforced inline in CI/script with:

- `GPL-*`, `AGPL-*`, `LGPL-*`, `SSPL-1.0`
