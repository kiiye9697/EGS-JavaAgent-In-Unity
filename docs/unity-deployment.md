# Unity Deployment

The Unity client is shipped as a reusable source folder:

`unity-client/Assets/EGS/JavaAgent`

Copy it into your target Unity project:

`<YourUnityProject>/Assets/EGS/JavaAgent`

## What Gets Deployed

- `Editor/`
- `Runtime/`
- `Embedded/egs-java-agent`
- `Embedded/jdk`

## Deployment Script

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\kiiye\Desktop\EGS-JavaAgent-In-Unity\scripts\deploy-unity-client.ps1 -UnityProjectRoot "D:\YourUnityProject"
```

## After Deployment

Open Unity and wait for script compilation, then verify:

1. `Project Settings -> EGS Java Agent`
2. `Window -> EGS Java Agent`
3. `Assets/EGS/JavaAgent/Embedded/egs-java-agent`
4. `Assets/EGS/JavaAgent/Embedded/jdk`

## Bundled Runtime Startup

The plugin can launch the bundled Java service from the Unity project:

- Java command: `Assets/EGS/JavaAgent/Embedded/jdk/bin/java.exe`
- Working directory: `Assets/EGS/JavaAgent/Embedded/egs-java-agent`
- Classpath: `lib/*`
- Main class: `com.egs.javaagent.JavaAgentApplication`

This keeps the runtime local to the Unity project after deployment.
