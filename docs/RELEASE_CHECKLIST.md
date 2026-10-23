# Release checklist (Unity app)

Merging to `develop` is not a release. The committed environment stays on `Local` until testing is done; a release build is made deliberately.

## Before a release build
1. Everything for this release is merged to `develop` and its checks passed.
2. **Tested on dev** in the editor, then **on a phone**: the changed flows, plus sign-in, car selection, the estimator and AR.
3. If the app depends on backend changes, they are already on the environment the build points at.
4. Only now set the default environment to Staging (`ApiSettings` / the environment enum), in its own commit.

## After the build
5. Install on a phone and sign in on a fresh install and an upgraded one (the saved sign-in must survive).
6. Try the main journey once: register, pick a car, customise, estimate, send to the dealer, request a test drive.

## If it goes wrong
Put the previous build back on the phones; set the environment back to Local in `develop`.
