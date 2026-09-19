# File Repository API

## Run

Requires .NET 10 SDK. From the repository root:

```bash
dotnet run --project src/FileRepo.Web
```

Files are stored in `./data/files` by default. Configure `FileRepository:RootPath`
and `FileRepository:MaxFileSizeBytes` in `appsettings.json` or with environment
variables such as `FileRepository__RootPath`.

Swagger UI is available at `/swagger`. The API exposes POST endpoints for
`/api/files/add`, `/update`, `/delete`, `/get`, and `/list`.

Example upload:

```bash
curl -F file=@README.md http://localhost:5000/api/files/add
curl -H 'Content-Type: application/json' -d '{"fileName":"README.md"}' \
  http://localhost:5000/api/files/get -o downloaded.md
```

## Validate

```bash
dotnet build FileRepository.slnx
dotnet test FileRepository.slnx
```