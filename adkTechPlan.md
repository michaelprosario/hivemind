# HiveMind Agent Development Kit Demo — Technical Plan

## Goal

Provide a small Google Agent Development Kit (ADK) demo that is served by
`adk web`, uses a Gemini model, answers questions from HiveMind's indexed data
and web search, and can explicitly store information back into HiveMind.

## Existing HiveMind integration

`src/HiveMind.Mcp` already exposes an MCP server over stdio (the default
transport). Its vector search tools include:

- `ListIndexes` and `InspectIndexSchema` for discovery.
- `Search` for semantic retrieval from a named Qdrant index.
- `StoreMemory` for writes, with optional idempotent key, tags, author, and
  source URL.
- `CreateIndex` for creating an index when needed.

The demo should consume these existing tools rather than duplicate persistence
or vector-search behavior. The MCP server expects Qdrant and the configured
embedding model to be available.

## Proposed structure

```text
examples/
  hivemind_adk/
    __init__.py
    agent.py
    requirements.txt
    .env.example
    README.md
adkTechPlan.md
```

The Python ADK project is kept separate from the .NET solution. This leaves
HiveMind's existing build and runtime dependencies unchanged.

## Agent design

1. **Root assistant** — an ADK `LlmAgent` configured with a Gemini Flash model
   (`gemini-flash-latest` by default). It decides whether the question calls
   for HiveMind retrieval, web research, or both; synthesizes results; and
   reports uncertainty rather than inventing tool outcomes.
2. **HiveMind MCP tools** — an ADK `McpToolset` connected to the existing
   `HiveMind.Mcp` .NET project over stdio. The process is started with
   `dotnet run --no-build` after a one-time build. Its working directory is the
   MCP project directory because the server's relative data/model paths are
   configured from there.
3. **Web research specialist** — a child ADK agent using Google's built-in
   Search tool. Wrapping search in an `AgentTool` keeps web-search capability
   separate from the HiveMind MCP tool list and asks it to return source URLs
   for the root agent's response.
4. **Memory writes** — the root agent calls the existing `StoreMemory` tool
   only when the user explicitly requests a write. It should state the target
   index and content clearly and use a stable key when updating an existing
   memory.

## Configuration and operation

- Install Python 3.10+ and `google-adk`; install the .NET 10 SDK and Docker
  Compose.
- Start Qdrant with `docker compose -f
  docker-compose/qdrant/docker-compose.yaml up -d`.
- Build `src/HiveMind.Mcp/HiveMind.Mcp.csproj` before starting ADK Web.
- Provide `GOOGLE_API_KEY` and optionally `GEMINI_MODEL` through the local
  `.env` file. Never commit actual credentials.
- Run `adk web .` from `examples/`, then select
  `hivemind_research_assistant` in the UI.
- Allow `HIVEMIND_REPO_ROOT` to override the repository location when running
  the sample outside its checked-out location.

## Validation

- Confirm the MCP subprocess starts and the agent can list available indexes.
- Query a known HiveMind index and verify the response is based on returned
  search results.
- Ask a current-events question and verify the web specialist returns source
  URLs that are preserved in the final response.
- Explicitly request a memory write, verify success from the tool response,
  then search for the saved content. Confirm the agent does not write for
  ordinary questions.
- Verify MCP protocol communication remains on stdio; the initial .NET build
  is performed separately so build output cannot be mistaken for MCP messages.

## Risks and constraints

- Google Search and Gemini API access require a valid Google API key and may
  incur API usage costs.
- Local retrieval and writes require Qdrant, the embedding model files, and
  compatible vector indexes to be available.
- The stdio process is local to the ADK host and has the permissions of that
  user; do not expose the ADK Web UI or allow untrusted users to invoke write
  tools without adding authentication and authorization controls.
- Keep the Gemini model configurable because model availability and aliases
  can change independently of HiveMind.
