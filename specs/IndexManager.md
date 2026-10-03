# Vector Index Management Plan

Add MCP tools that let an assistant inspect and create Qdrant collections for
task-specific vector search.

## `inspect_index_schema`

- **Input:** `collection_name`
- **Output:** the collection's payload schema, indexed payload fields, total
  point count, and vector count.
- **Purpose:** expose the actual stored fields and their types so the assistant
  can construct accurate payload filters (for example, distinguish
  `status: "open"` from `is_open: true`).
- **Acceptance criteria:**
  - The tool reports the requested collection's schema and counts.
  - The schema distinguishes field names and types where Qdrant provides that
    information.
  - Missing collections and inspection errors are reported explicitly.

## `create_index`

- **Input:** index `name` (Cosine).
- **Purpose:** create task-specific vector indexes on demand rather than
  requiring every task to reuse a shared index.
- **Acceptance criteria:**
  - The tool creates a index using the requested name and distance metric gets configured to cosine.
  - The index's vector configuration is compatible with the embedding dimensions used by the application.
  - Creation failures, including a name conflict, are reported explicitly.
