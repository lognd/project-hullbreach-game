# Contributing

This repository is private to Company of Theseus. These are the working
rules for the team; the platform repo follows the same ones.

## Branching

`main` is protected: it only moves by pull request, and the pull request
must be green. Nobody pushes to `main` directly, including the Scrum
Master. Branch protection is configured in the GitHub repository settings
(require a pull request and the `All checks pass` status check).

Branch from `main` for every piece of work, one concern per branch:

```
git switch main && git pull
git switch -c <type>/<short-description>      # feat/ship-builder, fix/hull-hp
```

Rebase onto `main` before opening the PR so the merge is linear. Squash or
rebase merges only; no merge commits.

## What "green" means

CI runs `scripts/check_unity_tree.sh` on every pull request and an
`All checks pass` job that fails if it did not succeed. That last job is
the single required status check. Run the same script locally before
pushing. Editor tests and a headless build are not in CI yet; see
`TODO.md`.

## Commits

Conventional commits: `feat:`, `fix:`, `chore:`, `docs:`, `test:`,
`refactor:`. Subject line under 72 characters, imperative mood. The body
explains why, not what -- the diff already says what.

## Unity-specific rules

- **Same editor version for everyone.** `ProjectSettings/ProjectVersion.txt`
  pins it and CI checks it. Upgrading the editor is its own PR that touches
  nothing else.
- **Commit `.meta` files with their assets, always.** Moving or renaming
  an asset in Unity moves the meta with it; doing it in a file explorer
  does not, and breaks every reference for everyone else.
- **Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or
  builds.** `.gitignore` covers them; CI fails if one slips through.
- **One person per scene or prefab at a time.** Scene and prefab YAML
  merges almost never go well. Say in chat which scene you are in. If you
  do collide, `git checkout --theirs` one side and redo the smaller change
  by hand.
- **Scripts live under `Assets/Scripts/`** in the folder for their area.
  Editor-only code goes in an `Editor/` folder so it is stripped from
  builds.
- **Server authority.** Gameplay state is simulated in the server world.
  Clients send input through ghost commands; anything a client could lie
  about must be validated or recomputed on the server.

## Secrets

No API keys, service credentials, or platform tokens in the repo. Unity
Gaming Services are linked per developer in the editor, and the server's
platform credentials arrive through the environment at deploy time.
