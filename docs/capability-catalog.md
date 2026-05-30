# Unity Agent Capability Catalog

This is the formal capability list for the current Unity + Java + LangChain4j agent runtime.

## Built-In Skill Profiles

1. `GeneralAgent`
2. `ShaderAuthoring`
3. `MaterialWorkflow`
4. `FunctionImplementation`
5. `CompileRepair`
6. `LogicCleanup`

## 1. Shader

Current capabilities:

- read `.shader`, `.cginc`, `.hlsl`
- list shader assets
- propose shader file creation or replacement
- read reference material first

## 2. Material

Current capabilities:

- inspect `.mat` assets as project files
- inspect nearby shader assets
- align material expectations with shader proposals

## 3. Function

Current capabilities:

- read `.cs` and `.asmdef`
- list function or script assets
- propose C# file creation or replacement
- use compiler feedback in repair turns

## 4. Validation

Current capabilities:

- inspect compile state
- inspect compiler messages
- feed compile diagnostics back into repair

## 5. Scene

Current capabilities:

- inspect active scene name
- inspect selected assets and objects
- focus implemented assets after approval

## 6. Project

Current capabilities:

- list directories
- read file ranges
- read local reference documents or URLs
- inspect discovered files and selected snippets

## Workflow

1. attach references
2. inspect project
3. generate proposal
4. approve proposal
5. apply locally
6. inspect compile result
7. repair if needed
