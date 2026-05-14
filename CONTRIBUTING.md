# Contributing

## Local loop

```bash
dotnet restore
dotnet build PDFFlatten.sln
dotnet test PDFFlatten.sln
```

## Expectations

- keep the public API small and obvious
- add or update tests with every behaviour change
- prefer narrow, reviewable pull requests
- document user-facing behaviour changes in `README.md` and `CHANGELOG.md`

## Release notes

Before tagging a release, update `CHANGELOG.md`.

## CI

Pull requests should leave the `ci.yml` workflow green before merge.
