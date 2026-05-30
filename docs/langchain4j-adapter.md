# LangChain4j Adapter

## Current Role

`LangChain4jLanguageModelGateway` is now a real agentic path, not just a wrapped chat call.

It currently supports:

1. model-driven file inspection
2. model-driven directory discovery
3. model-driven project overview reads
4. model-driven proposal creation for reviewed file writes

## Implemented Tool Surface

Read tools:

1. `project_overview`
2. `read_file`
3. `read_file_range`
4. `list_directory`

Proposal tools:

1. `suggest_create_file`
2. `suggest_replace_file`

## DeepSeek Compatibility

When:

1. provider is `deepseek`
2. gateway is `langchain4j`
3. configured model is `deepseek-v4-flash`

the effective model is mapped to `deepseek-chat`.

This avoids DeepSeek reasoning-mode replay requirements that `langchain4j 1.0.0-beta3` does not fully preserve during tool-calling turns.

## Runtime Behavior

During a normal request:

1. the model can inspect project evidence through tools
2. the model can register reviewable proposals
3. tool usage is surfaced back through `model_tool:*` execution entries

During an approval request:

1. the model is skipped
2. Java executes the approved action locally

## Why This Matters

This architecture keeps LangChain4j on the critical path for agent behavior, while keeping final file writes under explicit human approval.
