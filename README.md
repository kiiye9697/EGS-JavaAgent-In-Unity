# EGS Java Agent In Unity

Unity Editor + Java + LangChain4j 的本地智能辅助开发插件。
它的目标不是替你自动写完所有东西，而是把 Unity 开发里最麻烦的部分整理成一个可审批、可回滚、可调试的工作台。

## What It Does

- Unity 内可停靠工作台
- 审批队列和差异预览
- 调试控制台和编译反馈
- 本地 Java 服务 `GET /health`
- 本地 Java 服务 `POST /v1/agent/execute`
- DeepSeek / OpenAI / GLM provider 接入
- LangChain4j 工具调用
- 参考文档和 URL 注入
- 选中场景对象和项目文件上下文读取
- 轻量回滚历史
- 嵌入式 JDK + Java 服务随 Unity 项目部署

## Architecture

```text
Unity Editor
  -> HTTP request
  -> Java Agent Orchestrator
  -> LangChain4j Gateway
  -> Project read / reference read / proposal generation
  -> Unity approval queue
  -> Local write on approval
  -> Compile feedback + repair loop
```

## Repository Layout

- `java-agent/` Java 后端服务
- `unity-client/` Unity 插件源码
- `docs/` 设计、部署、验收和 API 文档
- `scripts/` 部署和辅助脚本
- `Reference/` 参考工程和资料
- `protocol/` 请求和响应协议

## Quick Start

### 1. Prepare Java

Use Java 21.

Known-good local paths:

- JDK: `C:\tmp\jdk21-portable\jdk-21.0.11+10`
- Gradle: `C:\tmp\gradle-portable\gradle-8.14.3`

### 2. Set provider variables

For DeepSeek:

```powershell
[Environment]::SetEnvironmentVariable("EGS_AGENT_PROVIDER", "deepseek", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_MODEL", "deepseek-v4-flash", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_GATEWAY", "langchain4j", "User")
[Environment]::SetEnvironmentVariable("DEEPSEEK_API_KEY", "sk-xxx", "User")
```

### 3. Build and start Java

```powershell
$env:JAVA_HOME='C:\tmp\jdk21-portable\jdk-21.0.11+10'
$env:Path='C:\tmp\jdk21-portable\jdk-21.0.11+10\bin;' + $env:Path
cd C:\Users\kiiye\Desktop\JavaAgent\java-agent
& 'C:\tmp\gradle-portable\gradle-8.14.3\bin\gradle.bat' installDist
.\build\install\egs-java-agent\bin\egs-java-agent.bat
```

Check:

- `http://localhost:8765/health`
- `http://localhost:8765/v1/agent/execute`

## Unity Setup

1. Copy `unity-client/Assets/EGS/JavaAgent` into your Unity project `Assets/EGS/JavaAgent`
2. Open Unity and wait for compile
3. Open:
   - `Project Settings -> EGS Java Agent`
   - `Window -> EGS Java Agent -> Workspace`
   - `Window -> EGS Java Agent -> Approval Queue`
   - `Window -> EGS Java Agent -> Debug Console`

## Typical Flow

1. Write a prompt in the workspace
2. Optionally attach reference files or URLs
3. Send request to Java agent
4. Review proposals in Approval Queue
5. Approve and apply
6. Watch compile feedback in Debug Console
7. Use repair loop if needed

## Documentation

- [Architecture](docs/architecture.md)
- [Approval Architecture](docs/approval-architecture.md)
- [Capability Catalog](docs/capability-catalog.md)
- [HTTP API](docs/http-api.md)
- [Java SDK Setup](docs/java-sdk-setup.md)
- [Unity Deployment](docs/unity-deployment.md)
- [Acceptance Guide](docs/acceptance-guide.md)
- [Reference Architecture](docs/reference-architecture.md)

## Notes

- This repository is intentionally structured for course work and GitHub展示.
- Do not commit API keys.
- The current product is strongest at script, shader, approval, validation, and rollback workflows.
