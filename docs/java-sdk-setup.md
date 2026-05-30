# Java SDK Setup

The Java agent targets Java 21.

## Known Good Local Toolchain

- JDK: `C:\tmp\jdk21-portable\jdk-21.0.11+10`
- Gradle: `C:\tmp\gradle-portable\gradle-8.14.3`

## Session Setup

```powershell
$env:JAVA_HOME='C:\tmp\jdk21-portable\jdk-21.0.11+10'
$env:Path='C:\tmp\jdk21-portable\jdk-21.0.11+10\bin;' + $env:Path
```

## Validate

```powershell
java -version
javac -version
```

## Build

```powershell
cd C:\Users\kiiye\Desktop\EGS-JavaAgent-In-Unity\java-agent
& 'C:\tmp\gradle-portable\gradle-8.14.3\bin\gradle.bat' installDist
```

## Start

```powershell
.\build\install\egs-java-agent\bin\egs-java-agent.bat
```

## Provider Variables

### DeepSeek

```powershell
[Environment]::SetEnvironmentVariable("EGS_AGENT_PROVIDER", "deepseek", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_MODEL", "deepseek-v4-flash", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_GATEWAY", "langchain4j", "User")
[Environment]::SetEnvironmentVariable("DEEPSEEK_API_KEY", "sk-xxx", "User")
```

### OpenAI

```powershell
[Environment]::SetEnvironmentVariable("EGS_AGENT_PROVIDER", "openai", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_MODEL", "gpt-5", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_GATEWAY", "http", "User")
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-xxx", "User")
```

### GLM

```powershell
[Environment]::SetEnvironmentVariable("EGS_AGENT_PROVIDER", "glm", "User")
[Environment]::SetEnvironmentVariable("EGS_AGENT_MODEL", "glm-4.7", "User")
[Environment]::SetEnvironmentVariable("GLM_API_KEY", "your-key", "User")
```
