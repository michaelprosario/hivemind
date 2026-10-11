import os
from pathlib import Path

from google.adk.agents import LlmAgent
from google.adk.tools import google_search
from google.adk.tools.agent_tool import AgentTool
from google.adk.tools.mcp_tool import McpToolset
from google.adk.tools.mcp_tool.mcp_session_manager import StdioConnectionParams
from mcp import StdioServerParameters


MODEL = os.getenv("GEMINI_MODEL", "gemini-flash-latest")
REPOSITORY_ROOT = Path(
    os.getenv("HIVEMIND_REPO_ROOT", Path(__file__).resolve().parents[2])
).resolve()
MCP_PROJECT = REPOSITORY_ROOT / "src" / "HiveMind.Mcp" / "HiveMind.Mcp.csproj"
MCP_WORKING_DIRECTORY = MCP_PROJECT.parent

web_research_agent = LlmAgent(
    name="web_research_agent",
    model=MODEL,
    instruction=(
        "Research the user's question using Google Search. Summarize the useful "
        "evidence and include the source URLs returned by search. Distinguish "
        "verified facts from uncertainty."
    ),
    tools=[google_search],
)

hivemind_mcp = McpToolset(
    connection_params=StdioConnectionParams(
        server_params=StdioServerParameters(
            command="dotnet",
            args=[
                "run",
                "--no-build",
                "--no-launch-profile",
                "--project",
                str(MCP_PROJECT),
            ],
            cwd=str(MCP_WORKING_DIRECTORY),
        )
    )
)

root_agent = LlmAgent(
    name="hivemind_research_assistant",
    model=MODEL,
    instruction=(
        "Answer the user's questions using HiveMind's indexed data, web research, "
        "or both as appropriate. Use list_indexes to discover available HiveMind "
        "indexes before searching when the right index is not known. Use the "
        "web_research_agent for current or external information, and preserve its "
        "source URLs in your answer. Never claim that a search or write succeeded "
        "unless the tool result confirms it. Only write to HiveMind when the user "
        "explicitly asks you to save information; before storing, make the target "
        "index and content clear, and use an idempotent key when the user requests "
        "an update to an existing memory."
    ),
    tools=[
        hivemind_mcp,
        AgentTool(agent=web_research_agent),
    ],
)
