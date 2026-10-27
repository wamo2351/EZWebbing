# Contributing to EZWebbing

Thanks for your interest in improving EZWebbing! This document explains how to
report problems and propose changes.

## Reporting bugs

Please open a GitHub Issue and include:

- Unity version and render pipeline
- EZWebbing version (see `EZWebbing/package.json`)
- Steps to reproduce, ideally with a minimal scene or script
- What you expected to happen, and what happened instead
- Console errors or stack traces, if any

## Suggesting features

Open an Issue describing the use case first. EZWebbing is intentionally
**data-agnostic**: the core runtime must not contain game-specific logic
(stage data, flags, dialogs, and so on). Game-specific behavior belongs in an
adapter that subscribes to the graph's events or sets
`WebbingGraph.ConnectionValidator`. Proposals that fit this model are much
easier to accept.

## Pull requests

1. Fork the repository and create a topic branch from `main`.
2. Make your change, keeping it focused on a single concern.
3. Open the project in Unity 2022.3 LTS (the minimum supported version) and
   confirm there are no compile errors or new console warnings.
4. If the change affects the public API or behavior, update
   `EZWebbing/Documentation~/API-Reference.md` and add an entry to
   `EZWebbing/CHANGELOG.md`.
5. Open the pull request and describe what changed and why.

### Code style

- Follow the style of the surrounding code (namespaces, naming, and the short
  header comment at the top of each script).
- Keep core runtime code under the `EZWebbing` namespace and free of
  dependencies on sample content or on TextMeshPro.
- Use `UnityEditor` APIs only in the `Editor/` assembly; the `Runtime/`
  assembly must compile for player builds.

### Unity-specific notes

- **Commit `.meta` files** together with their assets. Never regenerate or
  delete them for existing files; they hold the GUIDs that scenes and prefabs
  reference.
- Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or IDE files
  (see `.gitignore`).
- Avoid committing large binary assets unless necessary.

## License of contributions

By submitting a contribution, you agree that it will be licensed under the
same [MIT License](LICENSE) as the rest of the project.
