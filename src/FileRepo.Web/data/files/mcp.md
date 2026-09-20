Model Context Protocol (MCP) Production Implementation and Best Practices

Executive Summary

The Model Context Protocol (MCP) is a fundamental transformation in the generative AI landscape, serving as a universal open standard for connecting AI agents to enterprise systems, data sources, and tools. By replacing bespoke integrations with a single JSON-RPC interface, MCP enables AI applications to interact predictably with external environments.

This briefing document outlines the critical components of MCP, including its three core primitives—Tools, Resources, and Prompts—and details the two primary transport mechanisms: Stdio for local subprocesses and Streamable HTTP for remote deployments. Production readiness requires a "security-first" approach, mandating OAuth 2.1 for networked servers and strict adherence to protocol integrity, such as avoiding standard output (stdout) for logging in Stdio-based systems. Testing must evolve beyond unit logic to include protocol verification, integration with real dependencies, and agent evaluations to ensure models can effectively navigate tool schemas and descriptions.

Core MCP Concepts and Primitives

An MCP server provides a standardized way for AI applications to reach external systems through three distinct capabilities. Maintaining the separation of these "lanes" ensures predictability and safety.

Primitive	Controller	Description	Purpose
Tools	Model-controlled	Functions the LLM can call to take actions or fetch live data.	Mutating state, creating tickets, or running queries.
Resources	Application-controlled	Addressable, read-only context the client can load (e.g., files, DB records).	Providing static or dynamic context without side effects.
Prompts	User-controlled	Reusable message templates invoked deliberately by the user.	Encoding opinions or consistent workflows (e.g., "review code").

Tool Design and Safety

Tools represent the most significant work area and potential risk. They require:

* Typed Input Schemas: Usually defined via Zod or similar libraries to help the model understand arguments.
* Structured Output: While text content works for humans, agents require machine-readable structuredContent and outputSchema for precise parsing.
* Error Handling: Failures (e.g., invalid city in a weather tool) must be returned as data (isError: true) rather than throwing protocol-level exceptions, which break the connection.

Transport Protocols

MCP defines two methods for moving bytes between clients and servers. The choice of transport dictates the deployment model and security requirements.

1. Stdio (Standard Input/Output)

* Deployment: The server runs as a subprocess of the client.
* Use Case: Local desktop assistants, CLI agents, or IDE integrations.
* Security: Access is governed by the user's local permissions; no network surface or auth handshake is required.
* Critical Rule: Servers must never write non-MCP messages to stdout. Doing so (e.g., via console.log or print) corrupts the JSON-RPC stream. All logging must be directed to stderr.

2. Streamable HTTP

* Deployment: A networked service reachable via a URL, replacing the deprecated SSE-only transport.
* Use Case: Remote servers, multi-user environments, or cloud-native deployments.
* Security: Requires OAuth 2.1 and session management. It uses a "call-now, fetch-later" pattern for long-running tasks.
* Implementation: Requires enableDnsRebindingProtection and explicit allowedHosts to prevent malicious web pages from reaching local HTTP servers.

Production Best Practices for MCP Servers

Architecture and Design

* Bounded Context: Each server should represent a single microservice domain, exposing only cohesive capabilities.
* Stateless and Idempotent: Tools should be designed to handle retries or parallel requests by returning deterministic results for the same inputs.
* Decoupling: Prompts, tools, and resources should be independent. Reusable prompts should be stored server-side rather than hardcoded into tools.
* Semantic Versioning: Use semver for the server and individual tools to manage breaking changes in the surface area.

Security and Authorization

* OAuth 2.1: Mandatory for HTTP-based transports as of the March 2025 specification. MCP servers act as resource servers that validate tokens issued by an external authorization server.
* Resource Indicators (RFC 8707): These bind tokens to specific servers, preventing a compromised MCP server from replaying a token against a different service.
* Minimal Exposure: Never echo secrets in tool results or elicitation messages.
* Human-in-the-Loop: Use "elicitation" for confirming risky or high-cost actions, providing a machine-readable summary of intended changes.

Operational Excellence

* Instrumentation: Emit structured logs with correlation IDs, record latency, and provide "token-cost hints" where possible.
* Large Output Management: For large payloads, return URIs or handles to resources instead of inlining megabytes of data into a single tool result.
* Graceful Degradation: Since capabilities vary by host (e.g., some may not support the latest "elicitation" features), servers should implement feature flags and capability checks during the initial handshake.

Testing and Verification Framework

Testing MCP servers is uniquely challenging because the "caller" is a non-deterministic language model. Reliability requires four distinct levels of verification.

The Four Levels of Testing

1. Unit Testing: Verifying that tool handlers behave correctly in isolation.
2. Protocol Verification: Using tools like the MCP Inspector to ensure the server speaks JSON-RPC 2.0 correctly and adheres to transport specifications.
3. Integration Testing: Testing tools against real dependencies (databases, APIs) rather than mocks. This is critical as the "dependencies are the point."
4. Agent Evaluations (Evals): Testing if an AI agent can actually use the tools to complete tasks. This assesses whether tool names, descriptions, and schemas are clear enough for the model to navigate.

Common Testing Pitfalls

* Description Ambiguity: If tool descriptions are vague, the model may choose the wrong tool or provide incorrect arguments.
* Side Effects: Useful tools often mutate state. Testing requires safe environments (e.g., sandboxes or ephemeral environments) where side effects can be contained.
* Stateful Sequences: Agent sessions are conversational; testing must account for state that spans multiple calls, such as stale handles or cleanup bugs.

Implementation Standards Across SDKs

MCP is supported across various languages, each with specific requirements for production use.

Language	Key SDK/Tools	Production Requirement
TypeScript	@modelcontextprotocol/sdk	Must use type: "module" in package.json for ESM support.
Python	mcp[cli], uv	Requires Python 3.10+; use logging module to target stderr.
Java	Spring Boot Starters	Avoid System.out.println; use @Tool for auto-registration.
Rust	rmcp	Use eprintln! for logging; leverage macros for tool routing.
Go	github.com/modelcontextprotocol/go-sdk	Use log.Println or fmt.Fprintf(os.Stderr, ...) for Stdio logging.
C#	.NET 8 SDK	Use CreateEmptyApplicationBuilder to prevent unwanted console noise.

Troubleshooting and Debugging

When integrating with hosts like Claude for Desktop, developers should monitor specific logs:

* mcp.log: General logging regarding connection failures.
* mcp-server-SERVERNAME.log: Captures the stderr output from a specific server.

Integration Checklist:

* Ensure absolute paths are used in configuration files (claude_desktop_config.json).
* Verify that the server binary is compiled and runs independently before connecting it to a host.
* Fully quit and restart the host application after any configuration changes; simply closing a window is insufficient.
