# GitHub setup guide

This is the shortest safe path to getting **PDFFlatten** onto GitHub and publishing to NuGet.

## 1. Create the GitHub repository

In GitHub:

1. Click **New repository**.
2. Name it **PDFFlatten**.
3. Choose public or private.
4. **Do not** add a README, `.gitignore`, or licence there — this repo already has them.
5. Create the repository.

## 2. Push this local project

From the project root:

```bash
git init
git add .
git commit -m "Initial PDFFlatten library setup"
git branch -M main
git remote add origin git@github.com:jonnymuir/PDFFlatten.git
git push -u origin main
```

If you prefer HTTPS, use the URL GitHub shows you instead.

## 3. Optional but recommended branch setup

Create a `dev` branch if you want a clean integration branch for day-to-day work:

```bash
git checkout -b dev
git push -u origin dev
git checkout main
```

Then protect `main` in **Settings → Branches** and require pull requests.

## 4. Enable Actions

Open **Settings → Actions → General** and make sure workflows are allowed.

## 5. Add repository secrets

Open **Settings → Secrets and variables → Actions** and add:

- `NUGET_API_KEY` — your NuGet.org API key

That is the only required secret for publishing.

## 6. Verify CI

Push a normal commit or open a pull request. The `ci.yml` workflow should:

- restore dependencies
- build the solution
- run the tests
- produce package artifacts

## 7. Publish a release

Update `CHANGELOG.md`, commit, then tag a version:

```bash
git checkout main
git pull origin main
git tag v0.2.0
git push origin v0.2.0
```

The release workflow will:

1. restore, build, and test
2. pack `PDFFlatten`
3. create a GitHub release for the tag
4. push the package to NuGet.org

## 8. Install from another project

```bash
dotnet add package PDFFlatten
```

## If the repo owner or name changes

Update these values before publishing:

- `PackageProjectUrl`
- `RepositoryUrl`

They live in `src/PDFFlatten/PDFFlatten.csproj`.
