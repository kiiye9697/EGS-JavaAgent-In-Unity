# Unity Client

This folder contains the Unity Editor side of the EGS Java Agent plugin.

## What It Includes

- `Editor/` dockable windows, settings, approval queue, and debug console
- `Runtime/` request/response protocol and HTTP client

## How To Use

1. Copy `unity-client/Assets/EGS/JavaAgent` into your Unity project `Assets/EGS/JavaAgent`
2. Open Unity and wait for scripts to compile
3. Open:
   - `Project Settings -> EGS Java Agent`
   - `Window -> EGS Java Agent -> Workspace`
   - `Window -> EGS Java Agent -> Approval Queue`
   - `Window -> EGS Java Agent -> Debug Console`
4. Start the Java service locally
5. Send a prompt from the workspace
6. Review and approve proposals before local file writes happen

## Recommended Runtime

- Unity 2021.3 or newer
- Java 21
- Local Java agent service on `http://localhost:8765`

## Notes

- The Unity client is designed to be copied into a real Unity project.
- It is not a full Unity project by itself.
- The main goal is to keep the workflow dockable, inspectable, and easy to review.
