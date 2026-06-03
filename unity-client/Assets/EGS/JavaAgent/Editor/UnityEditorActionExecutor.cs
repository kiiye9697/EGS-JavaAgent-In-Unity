using System;
using System.Collections.Generic;
using EGS.JavaAgent.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EGS.JavaAgent.Editor
{
    internal static class UnityEditorActionExecutor
    {
        private const string TestCharacterName = "EGS_TestCharacter";
        private static readonly UnityToolDescriptor[] Tools =
        {
            new UnityToolDescriptor(
                "unity_create_test_character",
                "Scene",
                "Create a capsule test character with CharacterController, third-person camera and movement script.",
                "suggest_create_test_character",
                true
            ),
            new UnityToolDescriptor(
                "unity_focus_target",
                "Scene",
                "Select and ping a scene object or project asset by name/path.",
                "suggest_focus_target",
                false
            ),
            new UnityToolDescriptor(
                "unity_create_basic_material",
                "Material",
                "Create a basic Standard material asset for later assignment.",
                "suggest_create_basic_material",
                true
            ),
            new UnityToolDescriptor(
                "unity_assign_material_to_selection",
                "Material",
                "Assign a material asset to selected GameObjects with Renderers.",
                "suggest_assign_material_to_selection",
                true
            )
        };

        internal static IReadOnlyList<UnityToolDescriptor> ToolCatalog => Tools;

        internal static bool IsUnityEditorAction(string actionType)
        {
            return string.Equals(actionType, "suggest_create_test_character", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "suggest_focus_target", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "suggest_assign_material_to_selection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "suggest_create_basic_material", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryExecute(AgentApprovedAction action, out AgentActionExecutionResult result)
        {
            if (action == null)
            {
                result = Failure("unity_action", string.Empty, "Action is null.");
                return false;
            }

            try
            {
                switch (action.type)
                {
                    case "suggest_create_test_character":
                        result = CreateTestCharacter(action);
                        return true;
                    case "suggest_focus_target":
                        result = FocusTarget(action);
                        return true;
                    case "suggest_assign_material_to_selection":
                        result = AssignMaterialToSelection(action);
                        return true;
                    case "suggest_create_basic_material":
                        result = CreateBasicMaterial(action);
                        return true;
                    default:
                        result = Failure("unity_action", action.target, "Unsupported Unity editor action: " + action.type);
                        return false;
                }
            }
            catch (Exception exception)
            {
                result = Failure("unity_action", action.target, exception.Message);
                return true;
            }
        }

        internal static AgentSuggestedAction CreateTestCharacterSuggestion(string reason)
        {
            return new AgentSuggestedAction
            {
                type = "suggest_create_test_character",
                target = TestCharacterName,
                reason = reason,
                approvalRequired = true,
                proposalPreview =
                    "Approval required: yes\n"
                    + "Proposal type: Unity editor scene action\n"
                    + "Target: " + TestCharacterName + "\n\n"
                    + "This will create a capsule test character in the active scene, add a CharacterController, attach JavaAgentThirdPersonController, create a child follow Camera, select the character, and mark the scene dirty."
            };
        }

        internal static AgentSuggestedAction FocusTargetSuggestion(string target, string reason)
        {
            return new AgentSuggestedAction
            {
                type = "suggest_focus_target",
                target = target,
                reason = reason,
                approvalRequired = false,
                proposalPreview = "Focus target: " + target
            };
        }

        private static AgentActionExecutionResult CreateTestCharacter(AgentApprovedAction action)
        {
            GameObject existing = GameObject.Find(TestCharacterName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return Success("approved_create_test_character", TestCharacterName, "Existing test character selected.");
            }

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(capsule, "Create EGS test character");
            capsule.name = TestCharacterName;
            capsule.transform.position = FindSpawnPosition();

            CharacterController controller = capsule.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CharacterController>(capsule);
            }
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);

            var movement = capsule.GetComponent<JavaAgentThirdPersonController>();
            if (movement == null)
            {
                movement = Undo.AddComponent<JavaAgentThirdPersonController>(capsule);
            }

            GameObject cameraObject = new GameObject("ThirdPersonCamera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create EGS third person camera");
            cameraObject.transform.SetParent(capsule.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 2.2f, -4.5f);
            cameraObject.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            movement.FollowCamera = camera;

            Selection.activeGameObject = capsule;
            EditorGUIUtility.PingObject(capsule);
            EditorUtility.SetDirty(capsule);
            MarkSceneDirty();

            return Success("approved_create_test_character", TestCharacterName, "Created capsule character, third-person camera, CharacterController, and movement script.");
        }

        private static AgentActionExecutionResult FocusTarget(AgentApprovedAction action)
        {
            if (string.IsNullOrWhiteSpace(action.target))
            {
                return Failure("approved_focus_target", string.Empty, "Target is empty.");
            }

            UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizeAssetPath(action.target));
            if (target == null)
            {
                GameObject sceneObject = GameObject.Find(action.target);
                target = sceneObject;
            }

            if (target == null)
            {
                return Failure("approved_focus_target", action.target, "Target object or asset was not found.");
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            SceneView.lastActiveSceneView?.FrameSelected();
            return Success("approved_focus_target", action.target, "Focused target in Unity editor.");
        }

        private static AgentActionExecutionResult AssignMaterialToSelection(AgentApprovedAction action)
        {
            string materialPath = NormalizeAssetPath(action.target);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                return Failure("approved_assign_material", action.target, "Material asset was not found.");
            }

            int assigned = 0;
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                Undo.RecordObject(renderer, "Assign material from Java Agent");
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
                assigned++;
            }

            if (assigned == 0)
            {
                return Failure("approved_assign_material", action.target, "No selected GameObject has a Renderer.");
            }

            MarkSceneDirty();
            return Success("approved_assign_material", action.target, "Assigned material to " + assigned + " selected renderer(s).");
        }

        private static AgentActionExecutionResult CreateBasicMaterial(AgentApprovedAction action)
        {
            string materialPath = NormalizeAssetPath(action.target);
            if (string.IsNullOrWhiteSpace(materialPath) || !materialPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                materialPath = "Assets/EGS/JavaAgent/Generated/Materials/AgentMaterial.mat";
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
            {
                return Success("approved_create_basic_material", materialPath, "Material already exists.");
            }

            EnsureAssetFolder(System.IO.Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Material material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.82f, 0.9f, 1f, 1f)
            };
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            return Success("approved_create_basic_material", materialPath, "Created basic Standard material.");
        }

        private static Vector3 FindSpawnPosition()
        {
            if (Selection.activeGameObject != null)
            {
                return Selection.activeGameObject.transform.position + Vector3.up * 1.2f + Vector3.forward * 2f;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                return sceneView.pivot + Vector3.up * 1.2f;
            }

            return Vector3.up * 1.2f;
        }

        private static void MarkSceneDirty()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static string NormalizeAssetPath(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "Assets/" + normalized.TrimStart('/');
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static AgentActionExecutionResult Success(string type, string target, string output)
        {
            return new AgentActionExecutionResult
            {
                type = type,
                target = target,
                success = true,
                output = output
            };
        }

        private static AgentActionExecutionResult Failure(string type, string target, string output)
        {
            return new AgentActionExecutionResult
            {
                type = type,
                target = target,
                success = false,
                output = output
            };
        }
    }

    internal sealed class UnityToolDescriptor
    {
        internal UnityToolDescriptor(string name, string group, string description, string actionType, bool approvalRequired)
        {
            Name = name;
            Group = group;
            Description = description;
            ActionType = actionType;
            ApprovalRequired = approvalRequired;
        }

        internal string Name { get; }
        internal string Group { get; }
        internal string Description { get; }
        internal string ActionType { get; }
        internal bool ApprovalRequired { get; }
    }
}
