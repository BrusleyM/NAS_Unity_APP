#!/usr/bin/env python3
"""Check every GitHub Actions workflow in this repo against the NAS workflow rules.

    python3 .github/scripts/check_workflows.py            # check .github/workflows/*.yml
    python3 .github/scripts/check_workflows.py --self-test

The rules (and the reason for each) are in NAS_Backend/docs/WORKFLOW_RULES.md. Every workflow starts with
a line `# kind: ci | deploy | manual | scheduled`, and the kind decides what may trigger it.
Keep this file identical in all four repos.
"""
import glob
import os
import re
import sys

import yaml

KINDS = {"ci", "deploy", "manual", "scheduled"}
FLOATING_REFS = {"main", "master", "latest", "develop", "HEAD"}
CI_PUSH_BRANCHES = {"main", "develop"}


def triggers_of(doc):
    on = doc.get("on", doc.get(True))
    if isinstance(on, str):
        return {on: None}
    if isinstance(on, list):
        return {name: None for name in on}
    return on or {}


def check(name, text):
    """Return a list of 'name: RULE message' strings."""
    problems = []

    def bad(rule, message):
        problems.append(f"{name}: {rule} {message}")

    first = next((line for line in text.splitlines() if line.strip()), "")
    kind_match = re.match(r"#\s*kind:\s*(\w+)", first)
    kind = kind_match.group(1) if kind_match else None
    if kind not in KINDS:
        bad("W1", f"first line must be '# kind: {' | '.join(sorted(KINDS))}' (found {first[:40]!r})")

    doc = yaml.safe_load(text) or {}
    triggers = triggers_of(doc)

    if "permissions" not in doc:
        bad("W2", "declare top-level `permissions:` (least privilege; usually `contents: read`)")

    for job_id, job in (doc.get("jobs") or {}).items():
        if "timeout-minutes" not in job and "uses" not in job:
            bad("W3", f"job '{job_id}' needs `timeout-minutes` (a hung job otherwise runs for 6 hours)")

    if "pull_request_target" in triggers:
        bad("W4", "`pull_request_target` runs untrusted code with secrets; never use it")

    push = triggers.get("push")
    if "push" in triggers and not (push and (push.get("branches") or push.get("tags"))):
        bad("W5", "`push:` must name branches or tags explicitly")

    allowed = {
        "ci": {"pull_request", "push", "workflow_dispatch"},
        "deploy": {"workflow_dispatch", "push"},
        "manual": {"workflow_dispatch"},
        "scheduled": {"schedule", "workflow_dispatch"},
    }.get(kind)
    if allowed is not None:
        extra = set(triggers) - allowed
        if extra:
            bad("W6", f"a '{kind}' workflow may only be triggered by {sorted(allowed)}, not {sorted(extra)}")
        if kind == "ci" and push and push.get("tags"):
            bad("W6", "a 'ci' workflow must not run on tags (tags are for releases and deploys)")
        if kind == "ci" and push and set(push.get("branches") or []) - CI_PUSH_BRANCHES:
            bad("W6", f"a 'ci' workflow may run on pushes only to {sorted(CI_PUSH_BRANCHES)}; feature branches are covered by pull requests")
        if kind == "deploy" and push and push.get("branches"):
            bad("W6", "a 'deploy' workflow must never run on a branch push, only on tags or by hand")

    def concurrency_blocks():
        yield doc.get("concurrency")
        for job in (doc.get("jobs") or {}).values():
            yield job.get("concurrency")

    blocks = [c for c in concurrency_blocks() if c]
    if kind == "ci" and not (isinstance(doc.get("concurrency"), dict) and doc["concurrency"].get("cancel-in-progress") is True):
        bad("W7", "a 'ci' workflow needs top-level `concurrency` with `cancel-in-progress: true` (a newer push replaces an older run)")
    if kind == "deploy":
        if not blocks:
            bad("W7", "a 'deploy' workflow needs `concurrency` so two deploys never overlap")
        if any(isinstance(c, dict) and c.get("cancel-in-progress") is True for c in blocks):
            bad("W7", "a 'deploy' workflow must use `cancel-in-progress: false` (never abort a half-finished deploy)")

    for ref in re.findall(r"^\s*-?\s*uses:\s*([^\s#]+)", text, re.M):
        if ref.startswith("./") or ref.startswith("docker://"):
            continue
        action, _, version = ref.partition("@")
        if not version:
            bad("W8", f"action '{ref}' has no version; pin it (e.g. {action}@v4)")
        elif version in FLOATING_REFS:
            bad("W8", f"action '{ref}' follows a moving branch; pin a release tag")

    secrets = {s for s in re.findall(r"secrets\.([A-Za-z0-9_]+)", text) if s.upper() != "GITHUB_TOKEN"}
    if secrets and kind != "deploy":
        bad("W9", f"only 'deploy' workflows may read secrets (found {sorted(secrets)})")

    if kind == "deploy" and not re.search(r"dry[_-]?run", text):
        bad("W10", "a 'deploy' workflow must offer a dry run (an input named dry_run or a `dryrun` tag keyword)")

    return problems


def run(directory):
    files = sorted(glob.glob(os.path.join(directory, "*.yml")) + glob.glob(os.path.join(directory, "*.yaml")))
    problems = []
    for path in files:
        with open(path) as f:
            problems += check(os.path.basename(path), f.read())
    for p in problems:
        print("RULE BROKEN:", p)
    print(f"{len(files)} workflow(s) checked, {len(problems)} problem(s).")
    return 1 if problems else 0


GOOD_CI = """# kind: ci
name: x
on:
  pull_request:
  push:
    branches: [ main ]
permissions:
  contents: read
concurrency:
  group: g
  cancel-in-progress: true
jobs:
  t:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@v4
"""
GOOD_DEPLOY = """# kind: deploy
name: d
on:
  workflow_dispatch:
    inputs:
      dry_run: { type: boolean, default: true }
  push:
    tags: [ 'staging-*' ]
permissions:
  contents: read
jobs:
  d:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    concurrency: { group: deploy, cancel-in-progress: false }
    steps:
      - run: echo ${{ secrets.TOKEN }}
"""


def self_test():
    def rules(text):
        return {p.split()[1] for p in check("t.yml", text)}

    assert check("ci.yml", GOOD_CI) == [], check("ci.yml", GOOD_CI)
    assert check("d.yml", GOOD_DEPLOY) == [], check("d.yml", GOOD_DEPLOY)
    assert "W1" in rules(GOOD_CI.replace("# kind: ci\n", ""))
    assert "W2" in rules(GOOD_CI.replace("permissions:\n  contents: read\n", ""))
    assert "W3" in rules(GOOD_CI.replace("    timeout-minutes: 5\n", ""))
    assert "W4" in rules(GOOD_CI.replace("  pull_request:\n", "  pull_request_target:\n"))
    assert "W5" in rules(GOOD_CI.replace("  push:\n    branches: [ main ]\n", "  push:\n"))
    assert "W6" in rules(GOOD_CI.replace("branches: [ main ]", "tags: [ 'v*' ]"))
    assert "W6" in rules(GOOD_CI.replace("branches: [ main ]", "branches: [ main, feature/x ]"))
    assert "W6" in rules(GOOD_CI.replace("  pull_request:\n", "  schedule:\n    - cron: '0 3 * * *'\n"))
    assert "W6" in rules(GOOD_DEPLOY.replace("tags: [ 'staging-*' ]", "branches: [ main ]"))
    assert "W7" in rules(GOOD_CI.replace("cancel-in-progress: true", "cancel-in-progress: false"))
    assert "W7" in rules(GOOD_DEPLOY.replace("cancel-in-progress: false", "cancel-in-progress: true"))
    assert "W7" in rules(GOOD_DEPLOY.replace("    concurrency: { group: deploy, cancel-in-progress: false }\n", ""))
    assert "W8" in rules(GOOD_CI.replace("actions/checkout@v4", "actions/checkout"))
    assert "W8" in rules(GOOD_CI.replace("actions/checkout@v4", "actions/checkout@main"))
    assert "W9" in rules(GOOD_CI.replace("      - uses: actions/checkout@v4\n", "      - uses: actions/checkout@v4\n      - run: echo ${{ secrets.TOKEN }}\n"))
    assert "W10" in rules(GOOD_DEPLOY.replace("dry_run", "something"))
    print("self-test passed: the good examples pass and each rule catches its violation.")
    return 0


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "workflows")
    sys.exit(run(root))
