# deploy/

Workload manifests for pbs-browser. On every release, the `manifests` job in
`.github/workflows/container.yml` runs the `manifests` action from hwinther/reusable-workflows,
which:

1. renders this directory, with the image pinned to `tag@digest`;
2. validates the output with kubeconform;
3. pushes it as an OCI artifact to `ghcr.io/hwinther/pbs-browser/manifests:<version>`;
4. signs it with cosign (keyless).

On pull requests, `manifest-diff.yml` posts the rendered diff against the base branch as a PR
comment. PRs labelled `deploy-feature` also get `manifests:pr-<N>` from `pr-e2e.yml`: the same
manifests, pinned to the PR image and signed by that workflow. There is no preview variant: the
preview runs the real client against PBS behind Authelia, exactly like production. Don't commit
rendered output.

The cluster repo (hwinther/proxmox) consumes the artifact through a Flux `OCIRepository` and
`Kustomization`. It provides everything that isn't in here:

- the namespace (via `targetNamespace`), its keyfile-clone label and the network policies;
- the Secret;
- the variable values below (via `postBuild.substitute`).

## Contract

### Variables

CI fails if the rendered output contains a variable without a default that isn't listed here.
Flux would otherwise substitute an empty string.

| Variable | Default | Used for |
|---|---|---|
| `APP_HOST` | (required) | Ingress host and homepage links, e.g. `pbs-browser.mgmt.wsh.no` |

### Secrets

In the target namespace:

- `pbs-browser-pbs`, loaded with `envFrom`: `PBS_REPOSITORY`, `PBS_PASSWORD`,
  `PBS_ENCRYPTION_PASSWORD`, optionally `PBS_FINGERPRINT` and `PBS_NAMESPACE`.
- `pbs-encryption-keyfile` (key `keyfile`), mounted at `/etc/pbs/keyfile`. Optional.

### Cluster requirements

- An amd64 node (`proxmox-backup-client-static` has no arm64 build).
- The `authelia-production-authelia-forwardauth` Traefik middleware. The app has no auth of its
  own and trusts the headers it injects.
- Ingress to port 8080 from Traefik (host network), and egress to PBS.

### Resource names

`Deployment/pbs-browser`, `Service/pbs-browser`, `Ingress/pbs-browser`. Flux prunes anything that
disappears from the artifact, so rename deliberately.

## Render locally

`kubectl kustomize deploy` shows the manifests. With the contract check and kubeconform, as CI
runs them, use the action's script from a hwinther/reusable-workflows checkout (needs kubectl and
kubeconform on `PATH`):

```bash
RENDERS='pbs-browser.yaml=deploy' \
  bash ../reusable-workflows/.github/actions/manifests/scripts/render-all.sh . /tmp/manifests /tmp/renders.txt validate
```
