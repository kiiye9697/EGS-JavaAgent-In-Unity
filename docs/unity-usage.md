# Unity Usage Guide

## Open the Plugin

Use `Window > EGS Java Agent > Workspace`.

Use the `中文/EN` toolbar button to switch UI language.

## Configure API Key

Open `Edit > Project Settings > EGS Java Agent`.

For DeepSeek:

```text
Provider: deepseek
Gateway: langchain4j
Model: deepseek-v4-flash
Environment variable: DEEPSEEK_API_KEY
```

Paste the key into `Local API Key`, then click `Save Local API Key`. This stores the key in Unity `EditorPrefs`, not in project files.

## Run a Request

1. Click `Start Agent`.
2. Click `Check Agent`.
3. Select a skill profile.
4. Enter your request.
5. Optionally add reference document paths or URLs.
6. Click `Send`.
7. Open `Approval Queue`.
8. Review the proposal and approve it.
9. If a script was created, select a GameObject and use `Attach Last Script`.

## Debug and Rollback

Open `Window > EGS Java Agent > Debug Console`.

Available actions:

- `Repair Now`: sends current compiler errors back to the agent.
- `Rollback Last`: restores the latest applied file snapshot.
- `Rollback All`: restores all tracked applied changes.
- `Refresh Assets`: runs `AssetDatabase.Refresh()`.
- `Attach Script`: attaches the latest compiled MonoBehaviour to selected GameObjects.

## Expected Behavior

- If no API key is configured, the UI shows a warning and the backend returns setup guidance.
- If the Java service is not running, `Start Agent` tries to launch the bundled runtime path from settings.
- Generated file writes require approval unless safe auto-approval is enabled.
- Applied writes are tracked for rollback.
