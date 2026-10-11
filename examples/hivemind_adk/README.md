# HiveMind ADK demo

This Python demo runs in the ADK Web UI and connects to the HiveMind MCP server
over stdio. Its main agent can search HiveMind indexes and store memory. A
specialist agent uses Google Search for current information; keeping that tool
on its own agent avoids mixing it with the MCP tools.

## Prerequisites

- Python 3.10 or later
- .NET 10 SDK
- Docker Compose
- A Google AI Studio API key with Gemini API access

## Start the demo

From the repository root, start Qdrant and build the MCP server:

```bash
docker compose -f docker-compose/qdrant/docker-compose.yaml up -d
dotnet build src/HiveMind.Mcp/HiveMind.Mcp.csproj
```

Set up the Python environment and Google API key:

```bash
cd examples/hivemind_adk
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
cd ..
```

Edit `.env` to set `GOOGLE_API_KEY`, then export its values and start ADK Web
from the parent directory so it discovers the `hivemind_adk` agent package:

```bash
set -a
source hivemind_adk/.env
set +a
adk web .
```

Open the URL printed by ADK (normally `http://localhost:8000`) and select
`hivemind_research_assistant`. The MCP server is launched automatically as a
child process. The `--no-build` option keeps build output out of the stdio MCP
protocol stream; rebuild the project after changing its source.

The model defaults to `gemini-flash-latest`, an ADK-supported Flash alias suited
to interactive, cost-conscious tool use. Set `GEMINI_MODEL` in `.env` to choose
another Gemini model available to your API key.

## Try it

- “What indexes are available in HiveMind?”
- “Search the docs index for how memory storage works.”
- “Use web search to find recent updates to the MCP specification and cite
  your sources.”
- “Save a short note about our chosen demo architecture to the `agent_memory`
  index.”

The agent only writes memory when explicitly asked. The example assumes Qdrant
is available at the endpoint configured by the MCP server and that its local
embedding model files are present.
