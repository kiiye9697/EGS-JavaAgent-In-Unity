# Architecture

## Goal

Build a Unity Editor + Java agent workbench that can:

1. read project context
2. read reference documents or URLs
3. generate reviewable proposals
4. apply approved file changes locally
5. surface compile feedback
6. support rollback and repair

## Main Layers

### Unity Client

- dockable workspace
- approval queue
- debug console
- settings panel
- scene and selection awareness

### Java Agent

- HTTP server
- orchestration layer
- LangChain4j gateway
- memory log
- file read/write executors

### Reference Materials

- local docs
- URLs
- capability catalog
- approval and deployment docs

## Flow

```text
Unity prompt
  -> Java execute request
  -> project + reference inspection
  -> proposal generation
  -> Unity approval queue
  -> local write on approval
  -> compile feedback
  -> repair turn
```
