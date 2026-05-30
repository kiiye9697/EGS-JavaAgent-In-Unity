namespace EGS.JavaAgent.Editor
{
    internal enum JavaAgentSkillProfile
    {
        GeneralAgent,
        ShaderAuthoring,
        MaterialWorkflow,
        FunctionImplementation,
        CompileRepair,
        LogicCleanup
    }

    internal static class JavaAgentSkillCatalog
    {
        internal static string GetLabel(JavaAgentSkillProfile profile)
        {
            switch (profile)
            {
                case JavaAgentSkillProfile.ShaderAuthoring:
                    return "Shader Authoring";
                case JavaAgentSkillProfile.MaterialWorkflow:
                    return "Material Workflow";
                case JavaAgentSkillProfile.FunctionImplementation:
                    return "Function Implementation";
                case JavaAgentSkillProfile.CompileRepair:
                    return "Compile Repair";
                case JavaAgentSkillProfile.LogicCleanup:
                    return "Logic Cleanup";
                default:
                    return "General Agent";
            }
        }

        internal static string GetDescription(JavaAgentSkillProfile profile)
        {
            switch (profile)
            {
                case JavaAgentSkillProfile.ShaderAuthoring:
                    return "Bias the agent toward shader, HLSL, CGINC, and NPR implementation work.";
                case JavaAgentSkillProfile.MaterialWorkflow:
                    return "Bias the agent toward material, shader-property, and renderer-binding workflows.";
                case JavaAgentSkillProfile.FunctionImplementation:
                    return "Bias the agent toward MonoBehaviour, gameplay function, and utility script implementation.";
                case JavaAgentSkillProfile.CompileRepair:
                    return "Bias the agent toward minimal compile-error repair and validation loops.";
                case JavaAgentSkillProfile.LogicCleanup:
                    return "Bias the agent toward separating logic from editor glue, UI, and scene-coupled code.";
                default:
                    return "General-purpose Unity project reasoning with proposal, approval, and repair support.";
            }
        }

        internal static string GetInstructionPrefix(JavaAgentSkillProfile profile)
        {
            switch (profile)
            {
                case JavaAgentSkillProfile.ShaderAuthoring:
                    return "Skill profile: Shader Authoring. Prioritize shader, material, hlsl, cginc, NPR, and render-pipeline evidence. Read reference inputs first when present. Prefer the smallest safe shader-oriented proposal.";
                case JavaAgentSkillProfile.MaterialWorkflow:
                    return "Skill profile: Material Workflow. Prioritize material assets, shader-property alignment, renderer usage, and nearby shader files. If a material or shader is unclear, inspect project assets before proposing changes.";
                case JavaAgentSkillProfile.FunctionImplementation:
                    return "Skill profile: Function Implementation. Prioritize MonoBehaviour, function boundaries, compile-safe C# implementation, and attachment-ready scripts. Prefer existing files over new ones when reasonable.";
                case JavaAgentSkillProfile.CompileRepair:
                    return "Skill profile: Compile Repair. Focus on current compiler diagnostics, touched files, and the smallest possible repair. Do not broaden scope unless the compile evidence requires it.";
                case JavaAgentSkillProfile.LogicCleanup:
                    return "Skill profile: Logic Cleanup. Focus on extracting reusable logic from UI, scene glue, or editor orchestration. Prefer clearer responsibilities, testable functions, and lower coupling.";
                default:
                    return string.Empty;
            }
        }
    }
}
