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

## Vector search

Vector search indexes supported local text files into a caller-specified Qdrant
index (a Qdrant collection) and searches only the index supplied in the
request. The API uses the free, local `BAAI/bge-small-en-v1.5` ONNX model; it
does not send file content or queries to a hosted embedding service.

Before starting the API, download the pinned model artifacts into
`src/FileRepo.Web/data/models/bge-small-en-v1.5/` (or configure
`VectorSearch__ModelDirectory`). The directory must contain `model.onnx` and
the model's `vocab.txt`. Start local Qdrant with an API key:

```bash
export QDRANT_API_KEY=local-development-secret
docker compose -f docker-compose/qdrant/docker-compose.yaml up -d
dotnet run --project src/FileRepo.Web
```

The source file must be beneath `VectorSearch:AllowedSourceRoot` (default:
`./data/files`) and have a configured text extension. `vectorIndexName` is
required and must match `^[a-z0-9][a-z0-9_-]{0,62}$`.

```bash
curl -H 'Content-Type: application/json' \
  -d '{"vectorIndexName":"docs_v1","sourcePath":"example.md"}' \
  http://localhost:5000/api/vector-documents/ingest

curl -H 'Content-Type: application/json' \
  -d '{"vectorIndexName":"docs_v1","queryText":"How do dependencies point inward?","take":3}' \
  http://localhost:5000/api/vector-documents/search
```

## Validate

```bash
dotnet build FileRepository.slnx
dotnet test FileRepository.slnx
```