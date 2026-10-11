### Run single-process LiveKit quickstart

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/livekit/README.md

Installs prerequisites and launches the single-process LiveKit quickstart sample. Access the client at http://localhost:8080 to test voice and camera interactions.

```bash
pip install "google-adk[livekit]" fastapi uvicorn
python -m contributing.samples.integrations.livekit.quickstart
```

--------------------------------

### Install addlicense tool

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Installs Google's `addlicense` tool to enforce Apache 2.0 license headers. Requires Go to be installed.

```shell
go install github.com/google/addlicense@latest
```

--------------------------------

### Define few-shot examples with Example and ExampleTool in Agent

Source: https://github.com/google/adk-python/blob/main/docs/guides/examples/example/index.md

Build in-code examples using Example objects and attach them to an Agent via ExampleTool. Examples are provided as system instruction text on every turn alongside the agent instruction rather than functioning as callable tools.

```python
from google.adk.agents import Agent
from google.adk.examples import Example
from google.adk.tools import ExampleTool
from google.genai import types

example_tool = ExampleTool([
    Example(
        input=types.UserContent(parts=[types.Part(text="Where is order 4417?")]),
        output=[
            types.ModelContent(
                parts=[types.Part.from_function_call(
                    name="check_order_status", args={"order_id": "4417"}
                )]
            ),
            types.ModelContent(
                parts=[types.Part(text="Order 4417 shipped on Tuesday and arrives Friday.")]
            ),
        ],
    ),
    Example(
        input=types.UserContent(parts=[types.Part(text="I want a refund.")]),
        output=[
            types.ModelContent(
                parts=[types.Part(text="Sure — which order number is that for?")]
            )
        ],
    ),
])

agent = Agent(
    name="support_agent",
    instruction="Help the user with their order.",
    tools=[check_order_status, issue_refund, example_tool],
)
```

--------------------------------

### Install dependencies for two-process worker setup

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/livekit/README.md

Installs the required packages for the two-process architecture, including google-adk with LiveKit support, livekit-agents, FastAPI, and Uvicorn.

```bash
pip install "google-adk[livekit]" "livekit-agents>=1.4" fastapi uvicorn
```

--------------------------------

### Set up environment and install dependencies

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/authn-adk-all-in-one/README.md

Navigate to the integration sample directory, create a Python virtual environment, activate it, and install required packages.

```bash
# Go to the cloned directory
cd adk-python
# Navigate to the all in one authentication sample
cd contributing/samples/integrations/authn-adk-all-in-one/

python3 -m venv .venv

. .venv/bin/activate

pip install -r requirements.txt

```

--------------------------------

### Clone and install Tau-bench repository

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/README.md

Clones the Tau-bench repository and installs it in editable mode for running Tau-bench integration examples.

```bash
git clone https://github.com/sierra-research/tau-bench.git
cd tau-bench/
pip install -e .
```

--------------------------------

### Install google-adk with Slack support

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/slack/README.md

Installs the google-adk library with the required slack dependencies.

```bash
pip install "google-adk[slack]"
```

--------------------------------

### Start MCP server with client verification via filesystem_server.py

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_sse_mtls_agent/README.md

Sets environment variables to point to the certificate configuration and test CA, then starts the server on https://localhost:3000.

```bash
# Point to the certificate config
export GOOGLE_API_CERTIFICATE_CONFIG=$(pwd)/certificate_config.json

# Tell the server to trust our test CA for client verification
export SSL_CA_CERTS=$(pwd)/ca.crt

# Run the server
python filesystem_server.py
```

--------------------------------

### Install dependencies with uv

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/grok/README.md

Installs the required OpenAI and ADK extension dependencies from the repository root.

```bash
uv sync --extra extensions
```

--------------------------------

### Start Toolbox Server with --tools-file

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/toolbox_agent/README.md

Starts the Toolbox server on `http://127.0.0.1:5000` using the specified YAML configuration file.

```bash
toolbox --tools-file "tools.yaml"
```

--------------------------------

### Quick start OAuth2 server and run weather agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/oauth2_client_credentials/README.md

Sequential steps to start the mock OAuth2 server in the background, configure environment variables in `.env`, and query the weather assistant.

```bash
python contributing/samples/integrations/oauth2_client_credentials/oauth2_test_server.py &
```

```bash
# Choose Model Backend: 0 -> ML Dev, 1 -> Vertex
GOOGLE_GENAI_USE_ENTERPRISE=1

# ML Dev backend config
GOOGLE_API_KEY=your_google_api_key_here

# Vertex backend config
GOOGLE_CLOUD_PROJECT=your_project_id
GOOGLE_CLOUD_LOCATION=us-central1
```

```bash
# Ask for weather
python contributing/samples/integrations/oauth2_client_credentials/main.py "What's the weather in Tokyo?"
```

--------------------------------

### Install development tools with uv

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Installs `pre-commit` for linting/formatting and `tox` with the `tox-uv` plugin for isolated multi-version testing.

```shell
uv tool install pre-commit
uv tool install tox --with tox-uv
```

--------------------------------

### Install dependencies for custom web client

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gcp_auth/README.md

Navigates to the 3LO client directory and installs all required Python packages from requirements.txt.

```bash
cd contributing/samples/integrations/gcp_auth/client
pip install -r requirements.txt
```

--------------------------------

### Install LiveKit integration package

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/livekit/README.md

Install the Google ADK package with the LiveKit integration extra.

```bash
pip install "google-adk[livekit]"
```

--------------------------------

### Create virtual environment and install dependencies

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Initializes a Python 3.11 virtual environment using `uv`, activates it, and installs project dependencies including all extras.

```shell
uv venv --python "python3.11" ".venv"
source .venv/bin/activate
uv sync --all-extras
```

--------------------------------

### Start the local MCP filesystem server with uv

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_streamablehttp_agent/README.md

Run this command to start the local MCP server before running the agent.

```bash
uv run filesystem_server.py
```

--------------------------------

### Install dependencies with uv

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/files_retrieval_agent/README.md

Installs the required project dependencies including all extras required for the files retrieval agent sample.

```bash
uv sync --all-extras
```

--------------------------------

### Install Dependencies and Configure Authentication

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/interactions_api/README.md

Sets up the virtual environment and authentication credentials via either Google Cloud Project or Google API Key.

```bash
# From the adk-python root directory
uv sync --all-extras
source .venv/bin/activate

# Set up authentication (choose one):
# Option 1: Using Google Cloud credentials
export GOOGLE_CLOUD_PROJECT=your-project-id

# Option 2: Using API Key
export GOOGLE_API_KEY=your-api-key
```

--------------------------------

### Launch ADK Web UI for Sample Tools

Source: https://github.com/google/adk-python/blob/main/contributing/samples/tools/model_consult/README.md

Starts the ADK web interface pointed at the tools directory to test the model_consult sample visually.

```bash
adk web contributing/samples/tools
```

--------------------------------

### Start the hotel booking backend API

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/authn-adk-all-in-one/README.md

Activate the virtual environment in a separate shell and start the hotel booker application backend service.

```bash
# Go to the cloned directory
cd adk-python
# Navigate to the all in one authentication sample
cd contributing/samples/integrations/authn-adk-all-in-one/

# Activate Env for this shell
. .venv/bin/activate

cd hotel_booker_app/

# Start the hotel booker application
python main.py

```

--------------------------------

### Run the agent sample using adk web

Source: https://github.com/google/adk-python/blob/main/contributing/samples/live/live_non_blocking_tool_agent/README.md

Starts the live agent sample in the ADK web interface. Once started, trigger background tools via live voice input while continuing active conversation.

```bash
uv run adk web contributing/samples/live/live_non_blocking_tool_agent
```

--------------------------------

### Install LiteLLM

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/hello_world_nvidia/README.md

Install the LiteLLM dependency to enable ADK integration with NVIDIA NIM models.

```bash
pip install litellm
```

--------------------------------

### Start the ADK agent web interface

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/authn-adk-all-in-one/README.md

Activate the virtual environment, copy environment configuration, ensure the GOOGLE_API_KEY is configured, and launch the ADK agent web server.

```bash
# Go to the cloned directory
cd adk-python
# Navigate to the all in one authentication sample
cd contributing/samples/integrations/authn-adk-all-in-one/

# Activate Env for this shell
. .venv/bin/activate

cd adk_agents/

cp sample.env .env

# ⚠️ Make sure to update the API KEY (GOOGLE_API_KEY) in .env file

# Run the agent
adk web

```

--------------------------------

### Launch web UI for basic sample with adk web

Source: https://github.com/google/adk-python/blob/main/contributing/samples/plugins/plugin_reflect_tool_retry/README.md

Starts the ADK web interface for running and testing the basic retry plugin sample agent.

```bash
$ adk web contributing/samples/plugins/plugin_reflect_tool_retry
```

--------------------------------

### Example PostgreSQL connection string formats

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_postgres_agent/README.md

Shows sample URI formats for connecting to a local PostgreSQL instance or a hosted database service such as Supabase.

```text
postgresql://username:password@localhost:5432/mydb
postgresql://postgres.xyz:password@aws-region.pooler.supabase.com:5432/postgres
```

--------------------------------

### Running an agent with Runner and session setup (Before)

Source: https://github.com/google/adk-python/blob/main/contributing/samples/core/runner_debug_example/README.md

Standard multi-line execution pattern using InMemorySessionService and Runner.run_async with explicit session creation.

```python
from google.adk.sessions import InMemorySessionService
from google.genai import types

APP_NAME = "default"
USER_ID = "default"
session_service = InMemorySessionService()
runner = Runner(agent=agent, app_name=APP_NAME, session_service=session_service)
session = await session_service.create_session(
    app_name=APP_NAME, user_id=USER_ID, session_id="default"
)
content = types.Content(role="user", parts=[types.Part.from_text(text="Hi")])
async for event in runner.run_async(
    user_id=USER_ID, session_id=session.id, new_message=content
):
    if event.content and event.content.parts:
        print(event.content.parts[0].text)
```

--------------------------------

### Install google-adk with e2b extra

Source: https://github.com/google/adk-python/blob/main/contributing/samples/environment_and_skills/e2b_environment/README.md

Install the Google ADK package along with the E2B integration dependencies.

```bash
pip install google-adk[e2b]
```

--------------------------------

### Install Tau-bench and GEPA dependencies

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/gepa_tau_bench.ipynb

Clones the Google ADK and Tau-bench repositories, installs them in editable mode, and installs `gepa` and `retry` packages.

```python
# @title Install Tau-bench and GEPA
!git clone https://github.com/google/adk-python.git
!git clone https://github.com/sierra-research/tau-bench.git
!pip install -e ./adk-python --quiet

%cd tau-bench/
!pip install -e . --quiet

%cd ..
!pip install gepa --quiet

!pip install retry --quiet
```

--------------------------------

### Start the Local MCP Header Server with uv

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_dynamic_header_agent/README.md

Starts the local MCP server before running the dynamic header agent.

```bash
uv run header_server.py
```

--------------------------------

### Install dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_server_side_sampling/README.md

Install the required Python packages for running FastMCP server-side sampling.

```bash
pip install fastmcp openai litellm
```

--------------------------------

### Install google-adk with Daytona extra

Source: https://github.com/google/adk-python/blob/main/contributing/samples/environment_and_skills/daytona_environment/README.md

Installs the google-adk package with the optional Daytona integration dependencies.

```bash
pip install google-adk[daytona]
```

--------------------------------

### Launch the ADK Web UI for Integration Samples

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/api_registry_agent/README.md

Starts the ADK web server to run and interact with the agent from a browser. Access the UI at http://127.0.0.1:8000 and choose the api_registry_agent.

```bash
adk web contributing/samples/integrations

```

--------------------------------

### Execute Runner Debug Example Module

Source: https://github.com/google/adk-python/blob/main/contributing/samples/core/runner_debug_example/README.md

Runs the runner debug example main script containing eight demonstration scenarios.

```bash
python -m contributing.samples.core.runner_debug_example.main
```

--------------------------------

### Define text-only examples using dictionaries with ExampleTool

Source: https://github.com/google/adk-python/blob/main/docs/guides/examples/example/index.md

Pass plain dictionaries to ExampleTool for a more concise syntax when examples are text-only. ExampleTool automatically validates dictionary structures into Example objects.

```python
example_tool = ExampleTool([
    {
        "input": {"role": "user", "parts": [{"text": "Is 7 a prime number?"}]},
        "output": [{"role": "model", "parts": [{"text": "Yes, 7 is a prime number."}]}],
    },
])
```

--------------------------------

### Using run_debug with various options and configurations

Source: https://github.com/google/adk-python/blob/main/contributing/samples/core/runner_debug_example/README.md

Examples demonstrating minimal usage, multiple queries, custom session credentials, quiet event capture, verbose tool logging, and custom RunConfig.

```python
# Minimal usage
runner = InMemoryRunner(agent=agent)
await runner.run_debug("What's the weather?")

# Multiple queries
await runner.run_debug(["Query 1", "Query 2", "Query 3"])

# Custom session
await runner.run_debug(
    "Hello",
    user_id="alice",
    session_id="debug_session"
)

# Capture events without printing
events = await runner.run_debug(
    "Process this",
    quiet=True
)

# Show tool calls with verbose mode
await runner.run_debug(
    "What's the weather?",
    verbose=True  # Shows [Calling tool: ...] and [Tool result: ...]
)

# With custom configuration
from google.adk.agents.run_config import RunConfig
config = RunConfig(max_llm_calls=10)
await runner.run_debug("Query", run_config=config)
```

--------------------------------

### Launch workflow samples in Web UI

Source: https://github.com/google/adk-python/blob/main/contributing/samples/workflows/auth_oauth/README.md

Starts the ADK web interface to interactively test and run workflow samples.

```bash
adk web contributing/samples/workflows/
```

--------------------------------

### Start the ADK Web Server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/live/live_bidi_streaming_parallel_tools_agent/README.md

Execute this command in the terminal from the root directory containing the live_bidi_streaming_parallel_tools_agent folder to start the web server. Once running, access the web interface via the URL printed in the terminal (typically http://localhost:8000).

```bash
adk web

```

--------------------------------

### Install Chromium browser for Playwright

Source: https://github.com/google/adk-python/blob/main/contributing/samples/multimodal/computer_use/README.md

Downloads and installs the Chromium browser binary used by Playwright.

```bash
playwright install chromium

```

--------------------------------

### Install Agent Identity dependencies

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/agent_identity/README.md

Installs the Google ADK package with the agent-identity integration extras.

```bash
pip install "google-adk[agent-identity]"
```

--------------------------------

### Install required packages for PostgreSQL DatabaseSessionService

Source: https://github.com/google/adk-python/blob/main/contributing/samples/context_management/postgres_session_service/README.md

Installs the required Python packages including google-adk, asyncpg async driver, and greenlet.

```bash
pip install google-adk asyncpg greenlet

```

--------------------------------

### Install Dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_pr_triaging_agent/README.md

Upgrades pip and installs the required google-adk library for running the triaging agent.

```bash
pip install --upgrade pip
pip install google-adk
```

--------------------------------

### Launch the ADK web interface

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_in_agent_tool_remote/README.md

Starts the ADK web interface pointing to the MCP sample directory. Select mcp_in_agent_tool_remote from the interface to interact with the agent.

```bash
adk web contributing/samples/mcp
```

--------------------------------

### Install ADK with Slack Support via pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/slack_agent/README.md

Install the ADK package with the Slack integration dependencies included.

```bash
pip install "google-adk[slack]"

```

--------------------------------

### Start agent in interactive mode with adk web

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_answering_agent/README.md

Starts a local server and renders the agent's interactive web interface in a browser. Ensure all required environment variables are set before executing.

```bash
adk web
```

--------------------------------

### Install google-adk with GCP support

Source: https://github.com/google/adk-python/blob/main/docs/guides/integrations/model_armor/index.md

Installs the Google ADK package with Google Cloud Platform integration dependencies.

```shell
pip install 'google-adk[gcp]'
```

--------------------------------

### Complete SKILL.md file example

Source: https://github.com/google/adk-python/blob/main/docs/guides/skills/skill/index.md

Defines a complete SKILL.md file containing YAML frontmatter configuration and model instruction steps that reference local skill files and tools.

```markdown
---
name: weather-skill
description: A skill that provides weather information based on reference data and scripts.
metadata:
  adk_additional_tools:
    - get_wind_speed
---

Step 1: Check 'references/weather_info.md' for the current weather.
Step 2: If humidity is requested, run 'scripts/get_humidity.py' with the `location` argument.
Step 3: If wind speed is requested, use the `get_wind_speed` tool.
Step 4: Provide the complete weather update to the user.
```

--------------------------------

### Install locally built wheel file

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Installs the generated ADK wheel package into the active Python environment.

```shell
pip install dist/google_adk-<version>-py3-none-any.whl
```

--------------------------------

### Install google-adk MongoDB extra

Source: https://github.com/google/adk-python/blob/main/docs/guides/integrations/mongodb/mongodb_toolset/index.md

Installs the google-adk package with MongoDB support, bringing in the pymongo dependency.

```shell
pip install 'google-adk[mongodb]'
```

--------------------------------

### Install dependencies with OpenAI Responses extra

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/azure_responses_streaming/README.md

Install required dependencies from the repository root, including the OpenAI Responses extensions extra.

```bash
uv sync --extra extensions
```

--------------------------------

### Launch sample in interactive mode with adk run

Source: https://github.com/google/adk-python/blob/main/contributing/samples/multimodal/static_non_text_content/README.md

Starts the sample using ADK's built-in interactive CLI mode for free-form conversations.

```bash
cd contributing/samples
adk run static_non_text_content
```

--------------------------------

### Install pre-commit Git hooks

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Installs Git hooks to automatically run `isort`, `pyink`, `addlicense`, and `mdformat` on each commit.

```shell
pre-commit install
```

--------------------------------

### Start PostgreSQL with Docker Compose

Source: https://github.com/google/adk-python/blob/main/contributing/samples/context_management/postgres_session_service/README.md

Starts a local PostgreSQL instance in detached mode using Docker Compose.

```bash
docker compose up -d
```

--------------------------------

### Install Python dependencies with uv pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/multimodal/computer_use/README.md

Installs the required Python packages for the computer use agent from the sample requirements file.

```bash
uv pip install -r contributing/samples/computer_use/requirements.txt

```

--------------------------------

### Install google-adk dependencies

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gcp_auth/README.md

Install the google-adk package including optional extras for Agent Identity and MCP support.

```bash
pip install "google-adk[agent-identity,mcp]"
```

--------------------------------

### Launch the ADK Dev UI

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/grok/README.md

Starts the local ADK web interface to interactively test the Grok agent.

```bash
uv run --extra extensions adk web contributing/samples/models/grok
```

--------------------------------

### Install agent dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_documentation/adk_release_analyzer/README.md

Upgrades pip and installs the required google-adk package along with database dependencies.

```bash
pip install --upgrade pip
pip install google-adk[db]
```

--------------------------------

### Example interaction: simple dice rolling

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_basic/README.md

Shows an example dialogue interacting with the root agent to roll a die.

```text
User: Roll a 6-sided die
Bot: I rolled a 4 for you.
```

--------------------------------

### Start the local OAuth2 mock test server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/oauth2_client_credentials/README.md

Runs the mock OAuth2 server to test client credentials token exchange and provide protected weather endpoints.

```bash
python contributing/samples/integrations/oauth2_client_credentials/oauth2_test_server.py
```

--------------------------------

### Install agent dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_answering_agent/README.md

Upgrades pip and installs core libraries required by the agent, including google-adk and google-cloud-discoveryengine.

```bash
pip install --upgrade pip
pip install google-adk google-cloud-discoveryengine
```

--------------------------------

### Install ADK with OpenAI extra via pip

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/openai/README.md

Installs the google-adk package along with required OpenAI integration dependencies.

```bash
pip install "google-adk[openai]"
```

--------------------------------

### Run Live Workflow via ADK Web Interface

Source: https://github.com/google/adk-python/blob/main/contributing/samples/live/live_workflow/README.md

Starts the ADK web interface to interactively test the live voice workflow session.

```bash
uv run adk web contributing/samples/live/live_workflow
```

--------------------------------

### Install Playwright system dependencies for Chromium

Source: https://github.com/google/adk-python/blob/main/contributing/samples/multimodal/computer_use/README.md

Installs the necessary system-level dependencies required by Chromium for Playwright automation.

```bash
playwright install-deps chromium

```

--------------------------------

### Start MCP simple-tool server in SSE mode

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_in_agent_tool_remote/README.md

Starts the remote MCP simple-tool server using uvx on port 3000 using the SSE transport. Run this in a separate terminal before launching the ADK interface.

```bash
# Run the server using uvx (no installation needed)
# Port 3000 avoids conflict with adk web (which uses 8000)
uvx --from 'git+https://github.com/modelcontextprotocol/python-sdk.git#subdirectory=examples/servers/simple-tool' \
    mcp-simple-tool --transport sse --port 3000
```

--------------------------------

### Install google-adk GCP extra dependency

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/eventarc/generic_agent/README.md

Installs the google-adk package with the gcp extra required for Eventarc publishing.

```bash
pip install "google-adk[gcp]"
```

--------------------------------

### Download and Install MCP Toolbox Binary

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/toolbox_agent/README.md

Downloads the MCP Toolbox binary for the specified target OS and makes the binary executable.

```bash
export OS="linux/amd64" # one of linux/amd64, darwin/arm64, darwin/amd64, or windows/amd64
curl -O https://storage.googleapis.com/mcp-toolbox-for-databases/v1.1.0/$OS/toolbox
chmod +x toolbox
```

--------------------------------

### Install GCP Extra Dependency for ADK

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/eventarc/domain_specific_agent/README.md

Installs the google-adk package with GCP extras required for Eventarc publishing.

```bash
pip install "google-adk[gcp]"

```

--------------------------------

### Example query prompt for browser task

Source: https://github.com/google/adk-python/blob/main/contributing/samples/multimodal/computer_use/README.md

Sample prompt instructing the agent to navigate to Google Flights and search for flights.

```text
find me a flight from SF to Hawaii on next Monday, coming back on next Friday. start by navigating directly to flights.google.com

```

--------------------------------

### Run an ADK Agent with SlackRunner

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/slack/README.md

Initializes Slack AsyncApp and SlackRunner, then starts listening for events in Socket Mode using SLACK_BOT_TOKEN and SLACK_APP_TOKEN.

```python
import asyncio
import os
from google.adk.runners import Runner
from google.adk.integrations.slack import SlackRunner
from slack_bolt.app.async_app import AsyncApp

async def main():
    # 1. Initialize your ADK Runner (with your agent)
    # runner = Runner(agent=my_agent, session_service=my_session_service)

    # 2. Initialize Slack AsyncApp with your Bot Token
    slack_app = AsyncApp(token=os.environ["SLACK_BOT_TOKEN"])

    # 3. Initialize the SlackRunner
    slack_runner = SlackRunner(runner=runner, slack_app=slack_app)

    # 4. Start the runner in Socket Mode with your App Token
    await slack_runner.start(app_token=os.environ["SLACK_APP_TOKEN"])

if __name__ == "__main__":
    asyncio.run(main())
```

--------------------------------

### Install ADK and GEPA dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/README.md

Installs the Google ADK along with required dependencies including GEPA, Jinja2, LiteLLM, Abseil, and retry.

```bash
pip install google-adk absl-py gepa jinja2 litellm retry
```

--------------------------------

### Prompt for Project Structure Analysis

Source: https://github.com/google/adk-python/blob/main/src/google/adk/cli/built_in_agents/README.md

Example user prompt asking the assistant to review a project directory layout and provide organizational suggestions.

```text
Please analyze my existing project structure at './config_based/roll_and_check' and suggest improvements for better organization.
```

--------------------------------

### Start Remote A2A Agent Server with Uvicorn

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_root/README.md

Starts the remote Hello World A2A agent service on localhost port 8001 using uvicorn before running the main agent.

```bash
# Start the remote agent using uvicorn
uvicorn contributing.samples.a2a.a2a_root.remote_a2a.hello_world.agent:a2a_app --host localhost --port 8001
```

--------------------------------

### Install test-specific packages with pip

Source: https://github.com/google/adk-python/blob/main/tests/integration/integrations/agent_identity/README.md

Install the google-cloud-iamconnectorcredentials package needed specifically for GCP Agent Identity integration tests.

```bash
pip install google-cloud-iamconnectorcredentials
```

--------------------------------

### Install GEPA and Dependencies

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/voter_agent/gepa.ipynb

Installs the Google ADK Python repository, GEPA, litellm, and retry packages in a Colab environment.

```python
# @title Install GEPA
!git clone https://github.com/google/adk-python.git
!pip install -e ./adk-python --quiet
!pip install gepa --quiet
!pip install litellm --quiet
!pip install retry --quiet
```

--------------------------------

### Launch interactive web interface with adk web

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_documentation/adk_release_analyzer/README.md

Starts a local web server and interface for running the agent interactively to review recommendations before issue creation. Ensure the INTERACTIVE environment variable is set to 1 or left unset.

```bash
adk web contributing/samples/adk_team/adk_documentation
```

--------------------------------

### Configure and start the IDP service

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/authn-adk-all-in-one/README.md

Generate an RSA key pair, extract the public key to configure JWKS and private key in the environment, and run the Identity Provider application.

```bash
cd idp

# Create .env file by copying the existing one.
cp sample.env .env
cp sample.jwks.json jwks.json


# Carry out following steps
# 1. Generate a key pair, When asked about passphrase please press enter (empty passphrase)
ssh-keygen -t rsa -b 2048 -m PEM -f private_key.pem

# 2. Extract the public key
openssl rsa -in private_key.pem -pubout > pubkey.pub

# 3. Generate the jwks.json content using https://jwkset.com/generate and this public key (choose key algorithm RS256 and Key use Signature) (Please check the screenshot)
# 4. Update the jwks.json with the key jwks key created in 3 (please check the screenshot)
# 5. Update the env file with the private key
cat private_key.pem | tr -d "\n"
# 6. Carefully copy output of the command above into the .env file to update the value of PRIVATE_KEY
# 7. save jwks.json and .env

# Start the IDP
python app.py
```

--------------------------------

### Example NVIDIA NIM model identifiers

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/hello_world_nvidia/README.md

List example model identifiers supported by NVIDIA NIM, formatted with the nvidia_nim/ prefix.

```python
# Examples of available models
models = [
    "nvidia_nim/meta/llama3-8b-instruct",
    "nvidia_nim/meta/llama3-70b-instruct",
    "nvidia_nim/microsoft/phi-3-medium-4k-instruct",
    "nvidia_nim/mistralai/mistral-7b-instruct",
    "nvidia_nim/google/gemma-7b",
    "nvidia_nim/nvidia/nemotron-4-340b-instruct",
    # ... and many more
]
```

--------------------------------

### Set Google API Key Environment Variable

Source: https://github.com/google/adk-python/blob/main/contributing/samples/core/runner_debug_example/README.md

Sets the required GOOGLE_API_KEY environment variable before executing the example.

```bash
export GOOGLE_API_KEY="your-api-key"
```

--------------------------------

### Example interaction: combined operations

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_basic/README.md

Shows an example dialogue combining dice rolling with remote prime number verification.

```text
User: Roll a 10-sided die and check if it's prime
Bot: I rolled an 8 for you.
Bot: 8 is not a prime number.
```

--------------------------------

### Example interaction: YouTube search without authentication

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_auth/README.md

Shows sample root agent interaction delegating to the YouTube Search Agent without requiring user authentication.

```text
User: Search for 3 Taylor Swift music videos
Agent: I'll help you search for Taylor Swift music videos on YouTube.
[Agent delegates to YouTube Search Agent]
Agent: I found 3 Taylor Swift music videos:
1. "Anti-Hero" - Official Music Video
2. "Shake It Off" - Official Music Video
3. "Blank Space" - Official Music Video
```

--------------------------------

### Install youtube_search dependency

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/langchain_youtube_search_agent/README.md

Installs the required `youtube_search` dependency via `uv pip` to enable YouTube video search with Langchain.

```python
uv pip install youtube_search
```

--------------------------------

### Prompt to Create a New Agent

Source: https://github.com/google/adk-python/blob/main/src/google/adk/cli/built_in_agents/README.md

Example user prompt requesting the creation of a new agent with specific custom functionality.

```text
Create an agent that can roll n-sided number and check whether the rolled number is prime.
```

--------------------------------

### Start a live streaming session with InMemoryRunner and LiveRequestQueue

Source: https://github.com/google/adk-python/blob/main/docs/guides/runners/runner/live.md

Configures an LlmAgent with an InMemoryRunner and opens a bidirectional live streaming session using run_live. Allows pushing text or audio inputs via LiveRequestQueue while asynchronously processing streaming model events.

```python
root_agent = LlmAgent(
    name="voice_agent",
    instruction="You are a voice assistant. Answer queries concisely in spoken English.",
)

app = App(name="voice_app", root_agent=root_agent)
runner = InMemoryRunner(app=app)
queue = LiveRequestQueue()

# In an async task:
# Push user text or audio into the queue
queue.send_content(
    content=types.Content(
        role="user",
        parts=[types.Part.from_text(text="Hello! Can you hear me?")],
    )
)

async for event in runner.run_live(
    user_id="user_123",
    session_id="session_live",
    live_request_queue=queue,
):
  if event.content and event.content.parts:
    for part in event.content.parts:
      if part.text:
        print("Live model response:", part.text)
```

--------------------------------

### Prompt to Convert Existing Agent into Multi-Agent System

Source: https://github.com/google/adk-python/blob/main/src/google/adk/cli/built_in_agents/README.md

Example user prompt requesting the refactoring of an existing agent directory into a multi-agent architecture.

```text
Could you make the agent under `./config_based/roll_and_check` a multi agent system : root_agent only for request routing and two sub agents responsible for two functions respectively ?
```

--------------------------------

### Install CrewAI Tools Dependency

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/crewai_tool_kwargs/README.md

Installs the required crewai-tools package (requires Python 3.10+).

```bash
pip install 'crewai-tools>=0.2.0'
```

--------------------------------

### Start local LiveKit dev server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/livekit/README.md

Runs a local LiveKit server in development mode with fixed public credentials in a dedicated terminal.

```bash
livekit-server --dev
```

--------------------------------

### Start the LiveKit token and dispatch server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/livekit/README.md

Runs the token and dispatch server in a separate terminal to handle dispatches and serve the web client interface at http://localhost:8080.

```bash
python -m contributing.samples.integrations.livekit.client.main
```

--------------------------------

### Install upload script dependencies with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/adk_team/adk_answering_agent/README.md

Installs additional Python packages required specifically to run the document upload script.

```bash
pip install google-cloud-storage markdown
```

--------------------------------

### Installing Software Packages On Demand in E2BEnvironment

Source: https://github.com/google/adk-python/blob/main/docs/guides/integrations/e2b/e2b_environment/index.md

Install Python packages dynamically at runtime inside an E2B sandbox using env.execute(). This allows agents to adapt the environment on demand without creating a custom template for every scenario.

```python
from google.adk.integrations.e2b import E2BEnvironment

async def setup_custom_env(env: E2BEnvironment):
    # Install a specific version of pandas
    await env.execute("pip install pandas==2.2.0")

    # Verify the installation
    result = await env.execute("python -c 'import pandas; print(pandas.__version__)'")
    print(f"Installed version: {result.stdout}")
```

--------------------------------

### Run the basic plugin sample agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/plugins/plugin_basic/README.md

Execute the main.py script to run the sample agent with the basic plugin configured. Output displays plugin lifecycle counter logs alongside agent events.

```bash
python3 -m contributing.samples.plugins.plugin_basic.main
```

```bash
[Plugin] Agent run count: 1
[Plugin] LLM request count: 1
** Got event from hello_world
Hello world: query is [hello world]
** Got event from hello_world
[Plugin] LLM request count: 2
** Got event from hello_world
```

--------------------------------

### Example interaction: prime number checking

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_basic/README.md

Shows an example dialogue delegating a prime number check to the remote A2A agent.

```text
User: Is 7 a prime number?
Bot: Yes, 7 is a prime number.
```

--------------------------------

### Set OLLAMA_API_BASE and launch ADK web interface

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/hello_world_ollama/README.md

Sets the OLLAMA_API_BASE environment variable to point to the local Ollama server and starts the ADK web interface.

```bash
export OLLAMA_API_BASE="http://localhost:11434"
adk web
```

--------------------------------

### Launch ADK Web UI for MCP Toolset Auth Sample

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_toolset_auth/README.md

Run the ADK web interface to test the sample interactively and provide authentication credentials manually when prompted.

```bash
adk web contributing/samples/mcp/mcp_toolset_auth
```

--------------------------------

### Setup Vertex AI configuration and GEPA experiment parameters

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/gepa_tau_bench.ipynb

Sets up environment variables for Vertex AI enterprise access, configures model selections, defines GEPA hyperparameters, and adds a logging filter for inference warnings.

```python
# @title Setup
from datetime import datetime
import json
import logging
import os

import experiment as experiment_lib
from google.genai import types
import gepa_utils as utils

# @markdown ### ☁️ Configure Vertex AI Access
# @markdown Enter your Google Cloud Project ID and Location.

# @markdown Configure Vertex AI Access

GCP_PROJECT = ''  # @param {type: 'string'}
GCP_LOCATION = 'us-central1'  # @param {type: 'string'}

# @markdown ---
# @markdown ### 🧠 Configure LLM Models
# @markdown We recommend starting with Flash models for speed and cost-efficiency
# @markdown during optimization, but larger models like `gemini-2.5-pro` can also
# @markdown be used, especially for the reflection model.
AGENT_MODEL_NAME = 'gemini-2.5-flash'  # @param {type: 'string'}
USER_MODEL_NAME = 'gemini-2.5-flash'  # @param {type: 'string'}
REFLECTION_MODEL_NAME = 'gemini-2.5-pro'  # @param {type: 'string'}

# @markdown ---
# @markdown ### ⚙️ Configure Experiment Parameters
# @markdown Number of trajectories sampled from rollouts to be used by the reflection model in each GEPA step:
MINI_BATCH_SIZE = 8  # @param {type: 'integer'}
# @markdown Size of the pareto and feedback datasets (small setting for demo purposes):
MAX_DATASET_SIZE = 10  # @param {type: 'integer'}
# @markdown Number of times each task is run during evaluation:
NUM_EVAL_TRIALS = 4  # @param {type: 'integer'}
# @markdown Total budget for GEPA prompt evaluations:
MAX_METRIC_CALLS = 100  # @param {type: 'integer'}
# @markdown Maximum number of parallel agent-environment interactions
MAX_CONCURRENCY = 4  # @param {type: 'integer'}

# @markdown **Note:** You can find more information on how to configure GEPA in the [README file](https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gepa/README.md).

# The ADK uses these environment variables to connect to Vertex AI via the
# Google GenAI SDK.
os.environ['GOOGLE_GENAI_USE_ENTERPRISE'] = 'true'
os.environ['GOOGLE_CLOUD_PROJECT'] = GCP_PROJECT
os.environ['GOOGLE_CLOUD_LOCATION'] = GCP_LOCATION

# Set a logging verbosity suited for this experiment. See
# https://github.com/google/adk-python/issues/1852 for context
types.logger.addFilter(utils.FilterInferenceWarnings())
```

--------------------------------

### Start interactive chat via ADK web interface

Source: https://github.com/google/adk-python/blob/main/contributing/samples/context_management/static_instruction/README.md

Launch the local ADK web server to chat with Bingo in real-time, trigger feeding commands, and observe hunger state transitions.

```bash
cd contributing/samples/context_management
PYTHONPATH=../../../src adk web .
```

--------------------------------

### Example pizza ordering conversation flow

Source: https://github.com/google/adk-python/blob/main/contributing/samples/patterns/json_passing_agent/README.md

Illustrates the dialogue flow and tool execution between order intake and confirmation agents passing JSON data.

```text
[user]: I'd like a large pizza with pepperoni and mushrooms on a thin crust.
[order_intake_agent]: (tool call to get available sizes, crusts, toppings)
[order_intake_agent]: (returns a PizzaOrder JSON)
[order_confirmation_agent]: (tool call to calculate_price)
[order_confirmation_agent]: You ordered a large thin crust pizza with pepperoni and mushrooms. The total price is $15.00.
```

--------------------------------

### Start the Remote Approval Agent server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_human_in_loop/README.md

Start the remote A2A server serving the human-in-the-loop approval agent on port 8001.

```bash
# Start the remote a2a server that serves the human-in-the-loop approval agent on port 8001
adk api_server --a2a --port 8001 contributing/samples/a2a/a2a_human_in_loop/remote_a2a
```

--------------------------------

### Launch MCP simple-tool server using uvx

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_in_agent_tool_stdio/README.md

Runs the MCP simple-tool server directly from the GitHub repository's subdirectory using uvx over a stdio connection.

```bash
uvx --from 'git+https://github.com/modelcontextprotocol/python-sdk.git#subdirectory=examples/servers/simple-tool' \
    mcp-simple-tool
```

--------------------------------

### Launch ADK web interface for code execution agents

Source: https://github.com/google/adk-python/blob/main/contributing/samples/code_execution/custom_code_execution/README.md

Starts the ADK web interface to interactively select and run the custom_code_execution agent from the browser.

```bash
adk web contributing/samples/code_execution/
```

--------------------------------

### Example interaction: Dataset details retrieval with existing OAuth token

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_auth/README.md

Illustrates querying dataset details using an active OAuth session without repeating authentication.

```text
User: Show me details for my Customer Analytics dataset
Agent: I'll get the details for your Customer Analytics dataset.
[Using existing OAuth token]
Agent: Customer Analytics Dataset Details:
- Created: 2024-01-15
- Location: US
- Tables: 5
- Description: Customer behavior and analytics data
```

--------------------------------

### Start the ADK LiveKit worker process

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/livekit/README.md

Starts the ADK worker in a dedicated terminal. The worker registers with LiveKit and remains idle until a call requests agent participation.

```bash
python -m contributing.samples.integrations.livekit.livekit_worker start
```

--------------------------------

### Start remote prime agent server using adk api_server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_basic/README.md

Starts the remote A2A server serving the check prime agent on port 8001.

```bash
# Start the remote a2a server that serves the check prime agent on port 8001
adk api_server --a2a --port 8001 contributing/samples/a2a/a2a_basic/remote_a2a
```

--------------------------------

### Connect LiveKitRunner to a manually joined room

Source: https://github.com/google/adk-python/blob/main/src/google/adk/integrations/livekit/README.md

Initialize and start LiveKitRunner with an already-connected rtc.Room instance. Requires minting agent_token with with_kind("agent") and can_update_own_metadata in VideoGrants.

```python
from google.adk.integrations.livekit import LiveKitRunner

room = rtc.Room()
await room.connect(livekit_url, agent_token)

await LiveKitRunner(
    runner=runner,          # an unmodified ADK Runner
    room=room,
    user_id="alice",
    session_id=room_name,   # one room is one conversation
).start()
```

--------------------------------

### Activate virtual environment

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/gcp_auth/README.md

Navigate to the project directory, initialize a Python virtual environment, and activate it.

```bash
cd adk-python
python3 -m venv .venv
source .venv/bin/activate
```

--------------------------------

### Interact with the agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/patterns/json_passing_agent/README.md

Initial user input to start the pizza ordering conversation.

```text
I want to order a pizza
```

--------------------------------

### Run the agent with ADK web UI

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/integration_connector_euc_agent/README.md

Launch the ADK developer web interface from the repository root to interact with the sample agent.

```bash
adk web contributing/samples/integrations
```

--------------------------------

### Initializing Runner with an App or bare agent

Source: https://github.com/google/adk-python/blob/main/docs/guides/apps/app/index.md

Demonstrates current initialization using an App instance versus legacy agent wrapping. Passing an App preserves application-wide configuration and applies validation that bare agent initialization skips.

```python
# Current: the App carries the application-wide configuration.
runner = Runner(app=app, session_service=session_service)

# Legacy: the agent is wrapped in an App for you.
runner = Runner(
    app_name="weather_app",
    agent=root_agent,
    session_service=session_service,
)
```

--------------------------------

### Run parallel functions agent via ADK CLI

Source: https://github.com/google/adk-python/blob/main/contributing/samples/tools/parallel_functions/README.md

Start the parallel functions sample agent in interactive CLI mode or open the web interface.

```bash
# Start the agent in interactive mode
adk run contributing/samples/tools/parallel_functions

# Or use the web interface
adk web
```

--------------------------------

### Run the Files Retrieval Agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/files_retrieval_agent/README.md

Changes into the integrations sample directory and launches the agent via either the interactive CLI or the Web UI.

```bash
cd contributing/samples/integrations

# Interactive CLI
adk run files_retrieval_agent

# Web UI
adk web .
```

--------------------------------

### Start the Remote BigQuery Agent A2A server

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_auth/README.md

Launch the remote A2A API server exposing the BigQuery agent on port 8001.

```bash
# Start the remote a2a server that serves the BigQuery agent on port 8001
adk api_server --a2a --port 8001 contributing/samples/a2a/a2a_auth/remote_a2a
```

--------------------------------

### Visualize workflow sequence in Mermaid

Source: https://github.com/google/adk-python/blob/main/contributing/samples/workflows/sequence/README.md

Represents the unconditional execution flow from START through generate_fruit_agent to generate_benefit_agent.

```mermaid
graph TD
    START --> generate_fruit_agent
    generate_fruit_agent --> generate_benefit_agent
```

--------------------------------

### Configure MCP Server URL in agent.py

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_service_account_agent/README.md

Set the MCP server endpoint URL in agent.py before starting the agent.

```python
# agent.py
# TODO: Update this to the production MCP server url and scopes.
MCP_SERVER_URL = "https://test.sandbox.googleapis.com/mcp"
```

--------------------------------

### Install ADK Toolbox Dependency with pip

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/toolbox_agent/README.md

Installs the required `toolbox-adk` extension needed by ADK's `ToolboxToolset` class.

```bash
pip install google-adk[toolbox]
```

--------------------------------

### Sample File Structure

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/crewai_tool_kwargs/README.md

Directory layout showing key files in the crewai_tool_kwargs integration sample.

```text
crewai_tool_kwargs/
├── __init__.py       # Module initialization
├── agent.py          # Agent with CrewAI tool
├── main.py           # Happy path test
└── README.md         # This file
```

--------------------------------

### Run OAuth MCP Server and Test Script

Source: https://github.com/google/adk-python/blob/main/contributing/samples/mcp/mcp_toolset_auth/README.md

Start the OAuth-protected MCP server in one terminal, then execute the test client script in another terminal to observe the two-phase authentication flow.

```bash
PYTHONPATH=src python contributing/samples/mcp/mcp_toolset_auth/oauth_mcp_server.py
```

```bash
PYTHONPATH=src python contributing/samples/mcp/mcp_toolset_auth/main.py
```

--------------------------------

### Initialize SQLite Database with sqlite3 CLI

Source: https://github.com/google/adk-python/blob/main/contributing/samples/integrations/toolbox_agent/README.md

Creates and opens a new SQLite database file named `tool_box.db`.

```bash
sqlite3 tool_box.db
```

--------------------------------

### Expose an LlmAgent as an MCP server with to_mcp_server

Source: https://github.com/google/adk-python/blob/main/docs/guides/tools/mcp_tool/agent_to_mcp/index.md

Defines an agent with a tool and exposes it as a single MCP tool using `to_mcp_server`. Running the script starts the FastMCP server over the stdio transport.

```python
import random

from google.adk.agents import LlmAgent
from google.adk.tools.mcp_tool import to_mcp_server


def roll_die(sides: int) -> int:
  """Roll a die with the given number of sides and return the result."""
  return random.randint(1, sides)


dice_agent = LlmAgent(
    name="dice_agent",
    description="Rolls dice with any number of sides and reports the outcome.",
    instruction="Use the roll_die tool to roll the dice the user asks for.",
    tools=[roll_die],
)

# The whole agent becomes one MCP tool named "dice_agent".
server = to_mcp_server(dice_agent)

if __name__ == "__main__":
  server.run(transport="stdio")
```

--------------------------------

### Copy sample environment configuration file

Source: https://github.com/google/adk-python/blob/main/contributing/samples/models/hello_world_apigeellm/README.md

Copies the template environment file to create a local .env configuration file in the project directory.

```bash
cp .env-sample .env
```

--------------------------------

### Create and activate clean virtual environment for testing

Source: https://github.com/google/adk-python/blob/main/CONTRIBUTING.md

Sets up and activates a fresh virtual environment to test the locally built wheel distribution.

```shell
VENV_PATH=~/venvs/adk-quickstart
```

```shell
command -v deactivate >/dev/null 2>&1 && deactivate
```

```shell
rm -rf $VENV_PATH \
  && python3 -m venv $VENV_PATH \
  && source $VENV_PATH/bin/activate
```

--------------------------------

### Create, chat with, and delete a custom managed agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/managed_agent/custom_agent/README.md

Run the agent lifecycle commands to provision the custom agent resource, initiate a multi-turn chat, and delete the resource when finished. Because creation is asynchronous, wait a moment after creation before starting the first turn.

```bash
# 1. Create the custom agent (once).
python contributing/samples/managed_agent/custom_agent/agent.py --create

# 2. Chat with it. Provisioning can take a few minutes (longer for the first
#    agent in a project), so wait a moment after --create before the first turn.
adk run contributing/samples/managed_agent/custom_agent
#    or: adk web

# 3. Delete it when done.
python contributing/samples/managed_agent/custom_agent/agent.py --delete
```

--------------------------------

### Implement fan-out and fan-in workflow with JoinNode

Source: https://github.com/google/adk-python/blob/main/docs/guides/workflow/join_node/index.md

Demonstrates running multiple parallel tasks from START and synchronizing them with a JoinNode. The join node waits for all predecessor tasks to finish and passes an aggregated dictionary keyed by task name to the downstream node.

```python
from typing import Any
from google.adk import Event, Workflow
from google.adk.workflow import JoinNode, START

# Define parallel tasks
def make_uppercase(node_input: str) -> str:
  return node_input.upper()

def count_characters(node_input: str) -> int:
  return len(node_input)

def reverse_string(node_input: str) -> str:
  return node_input[::-1]

# Define the JoinNode
join_node = JoinNode(name="join_for_results")

# Define the aggregation node
async def aggregate(node_input: dict[str, Any]):
  yield Event(
      message=(
          f"Uppercase: {node_input['make_uppercase']}\n"
          f"Character Count: {node_input['count_characters']}\n"
          f"Reversed: {node_input['reverse_string']}\n"
      ),
  )

# Build the workflow
root_agent = Workflow(
    name="root_agent",
    edges=[(
        START,
        (make_uppercase, count_characters, reverse_string),
        join_node,
        aggregate,
    )],
)
```

--------------------------------

### Evaluate the agent using adk eval

Source: https://github.com/google/adk-python/blob/main/contributing/samples/live/live_non_blocking_tool_agent/README.md

Runs automated live evaluation using an audio user simulator with the specified evalset and configuration file. Requires installing the eval extra and configuring Vertex AI credentials in a .env file.

```bash
uv run adk eval \
  contributing/samples/live/live_non_blocking_tool_agent \
  contributing/samples/live/live_non_blocking_tool_agent/live_non_blocking_tool_agent.evalset.json \
  --config_file_path contributing/samples/live/live_non_blocking_tool_agent/test_config.json
```

--------------------------------

### Example interaction: BigQuery operations requiring OAuth authentication

Source: https://github.com/google/adk-python/blob/main/contributing/samples/a2a/a2a_auth/README.md

Demonstrates the OAuth flow triggered when requesting access to BigQuery datasets through the root agent.

```text
User: List my BigQuery datasets
Agent: I'll help you access your BigQuery datasets. This requires authentication with your Google account.
[Agent delegates to BigQuery Agent]
Agent: To access your BigQuery data, please complete the OAuth authentication.
[OAuth flow initiated - user redirected to Google authentication]
User: [Completes OAuth flow in browser]
Agent: Authentication successful! Here are your BigQuery datasets:
- dataset_1: Customer Analytics
- dataset_2: Sales Data
- dataset_3: Marketing Metrics
```

--------------------------------

### Instantiate an Agent with tools using google.adk.agents.llm_agent.Agent

Source: https://github.com/google/adk-python/blob/main/contributing/samples/core/quickstart/README.md

Configure an agent instance by passing tool functions to the tools parameter list along with descriptive instructions.

```python
from google.adk.agents.llm_agent import Agent

root_agent = Agent(
    name="weather_time_agent",
    description=(
        "Agent to answer questions about the time and weather in a city."
    ),
    instruction=(
        "I can answer your questions about the time and weather in a city."
    ),
    tools=[get_weather, get_current_time],
)
```

--------------------------------

### Configure ContainerCodeExecutor with LlmAgent

Source: https://github.com/google/adk-python/blob/main/docs/guides/code_executors/code_executor/index.md

Starts a local or self-hosted Docker container with network access disabled to isolate model code execution from the host environment.

```python
from google.adk.agents import LlmAgent
from google.adk.code_executors import ContainerCodeExecutor

agent = LlmAgent(
    name="data_analyst",
    instruction="Analyze data using Python scripts.",
    code_executor=ContainerCodeExecutor(
        image="python:3.11-slim",
        network_enabled=False,
        timeout_seconds=60,
    ),
)
```