# Acceptance Guide

This is the recommended end-to-end test flow.

## Environment

- JDK: `C:\tmp\jdk21-portable\jdk-21.0.11+10`
- Gradle: `C:\tmp\gradle-portable\gradle-8.14.3`

## Start Java

```powershell
$env:JAVA_HOME='C:\tmp\jdk21-portable\jdk-21.0.11+10'
$env:Path='C:\tmp\jdk21-portable\jdk-21.0.11+10\bin;' + $env:Path
cd C:\Users\kiiye\Desktop\EGS-JavaAgent-In-Unity\java-agent
& 'C:\tmp\gradle-portable\gradle-8.14.3\bin\gradle.bat' installDist
.\build\install\egs-java-agent\bin\egs-java-agent.bat
```

## Check Health

Open:

`http://localhost:8765/health`

Expected:

- `success = true`

## Unity Flow

1. Open `Project Settings -> EGS Java Agent`
2. Click `Use Recommended DeepSeek + LangChain4j Defaults`
3. Open `Window -> EGS Java Agent -> Workspace`
4. Paste a request such as:

```text
Create a Unity MonoBehaviour scaffold under Assets/Scripts and inspect nearby files first.
```

5. Review the proposal in `Approval Queue`
6. Approve and apply
7. Open `Debug Console` to inspect compile and action results

## Expected Outcome

- proposal preview appears
- approval queue receives a reviewable item
- local file write happens only after approval
- compile feedback is visible in the debug window
