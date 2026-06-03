using System.Collections.Generic;
using UnityEditor;

namespace EGS.JavaAgent.Editor
{
    internal enum JavaAgentLanguage
    {
        English,
        Chinese
    }

    internal static class LocalizationSystem
    {
        private const string LanguagePrefsKey = "EGS.JavaAgent.Language";

        private static readonly Dictionary<string, (string English, string Chinese)> Text = new()
        {
            ["workspace.title"] = ("EGS Java Agent Workspace", "EGS Java Agent 工作台"),
            ["workspace.status"] = ("Agent", "服务"),
            ["workspace.settings"] = ("Settings", "设置"),
            ["workspace.language"] = ("Language", "语言"),
            ["action.primary"] = ("Primary Actions", "主要操作"),
            ["action.start"] = ("Start Agent", "启动服务"),
            ["action.restart"] = ("Restart Agent", "重启服务"),
            ["action.check"] = ("Check Agent", "检查服务"),
            ["action.send"] = ("Send", "发送"),
            ["action.approval"] = ("Approval Queue", "审批队列"),
            ["action.debug"] = ("Debug Console", "调试控制台"),
            ["action.attach"] = ("Attach Last Script", "挂载脚本"),
            ["action.token"] = ("Open Token Settings", "打开密钥设置"),
            ["status.live"] = ("Live Workflow", "实时流程"),
            ["status.workflow"] = ("Status", "状态"),
            ["status.skill"] = ("Skill Profile", "技能配置"),
            ["status.selection"] = ("Selection", "选择对象"),
            ["status.pending"] = ("Pending Approvals", "待审批"),
            ["status.rollback"] = ("Rollback Records", "回退记录"),
            ["status.compile"] = ("Compile State", "编译状态"),
            ["status.runtime"] = ("Runtime", "运行时"),
            ["node.flow"] = ("Node Flow", "节点流程"),
            ["node.selected"] = ("Selected Node", "选中节点"),
            ["node.skill"] = ("Skill", "技能"),
            ["node.reference"] = ("Reference", "参考"),
            ["node.inspect"] = ("Inspect", "检查"),
            ["node.approve"] = ("Approve", "审批"),
            ["node.apply"] = ("Apply", "应用"),
            ["node.repair"] = ("Repair", "修复"),
            ["node.select"] = ("Select", "选择"),
            ["node.selectedButton"] = ("Selected", "已选择"),
            ["prompt.title"] = ("Request Composer", "需求输入"),
            ["prompt.mode"] = ("Mode", "模式"),
            ["prompt.skill"] = ("Built-in Skill", "内置技能"),
            ["prompt.references"] = ("Reference Inputs", "参考资料"),
            ["prompt.referenceHelp"] = ("One local file path or URL per line. The agent reads these references before implementation.", "每行填写一个本地文件路径或 URL。Agent 会在实现前读取这些参考资料。"),
            ["execution.title"] = ("Execution Controls", "执行控制"),
            ["execution.help"] = ("Built-in skills guide shader, material, function, validation, scene, and project work through the same approval and repair loop.", "内置技能会把 Shader、Material、Function、Validation、Scene、Project 工作导入同一套审批和修复流程。"),
            ["execution.send"] = ("Send To Java Agent", "发送给 Java Agent"),
            ["execution.repair"] = ("Trigger Repair From Compiler Errors", "根据编译错误修复"),
            ["execution.last"] = ("Last Applied Asset", "最近应用资源"),
            ["response.title"] = ("Agent Transcript", "Agent 输出"),
            ["response.raw"] = ("Raw", "原文"),
            ["targets.title"] = ("Implemented Targets", "已实现目标"),
            ["targets.empty"] = ("No applied targets yet. After an approved write, focus or open the implemented asset here.", "还没有已应用目标。审批写入后可在这里定位或打开资源。"),
            ["issues.title"] = ("Latest Issues", "最新问题"),
            ["issues.empty"] = ("No lightweight project issues were reported in the latest run.", "最近一次运行没有报告轻量级项目问题。"),
            ["history.title"] = ("Recent Requests", "最近请求"),
            ["history.empty"] = ("No request history yet.", "暂无请求历史。"),
            ["warning.key"] = ("Provider API key is not configured for {0}. Real LangChain4j model/tool execution will not work until {1} is available.", "{0} 的 API Key 尚未配置。只有配置 {1} 后，真实 LangChain4j 模型和工具执行才会工作。"),
            ["settings.title"] = ("EGS Java Agent", "EGS Java Agent"),
            ["settings.endpoint"] = ("Endpoint", "端点"),
            ["settings.session"] = ("Session ID", "会话 ID"),
            ["settings.mode"] = ("Default Mode", "默认模式"),
            ["settings.provider"] = ("Provider", "提供商"),
            ["settings.gateway"] = ("Gateway", "网关"),
            ["settings.model"] = ("Model", "模型"),
            ["settings.env"] = ("Use Environment Token", "使用环境变量密钥"),
            ["settings.token"] = ("Provider Token", "提供商密钥"),
            ["settings.envName"] = ("Environment Variable", "环境变量名"),
            ["settings.localKey"] = ("Local API Key", "本地 API Key"),
            ["settings.saveKey"] = ("Save Local API Key", "保存本地 API Key"),
            ["settings.clearKey"] = ("Clear Local API Key", "清除本地 API Key"),
            ["settings.automation"] = ("Approval And Debug Automation", "审批与调试自动化"),
            ["settings.autoApprove"] = ("Auto Approve Create Files", "自动审批新建文件"),
            ["settings.autoRepair"] = ("Auto Repair On Compile Error", "编译错误自动修复"),
            ["settings.maxRepair"] = ("Max Auto Repair Attempts", "最大自动修复次数"),
            ["settings.autoAttach"] = ("Auto Attach Applied Script", "自动挂载已应用脚本"),
            ["settings.launch"] = ("Local Agent Launch", "本地服务启动"),
            ["settings.java"] = ("Java Command", "Java 命令"),
            ["settings.workingDir"] = ("Working Directory", "工作目录"),
            ["settings.classpath"] = ("Classpath", "类路径"),
            ["settings.mainClass"] = ("Main Class", "主类"),
            ["settings.defaults"] = ("Use Recommended DeepSeek + LangChain4j Defaults", "使用推荐 DeepSeek + LangChain4j 默认值"),
            ["settings.save"] = ("Save", "保存"),
            ["settings.keyOk"] = ("API key is configured. Starting the bundled Java service from Unity injects it into the provider environment variable.", "API Key 已配置。从 Unity 启动内置 Java 服务时会自动注入到对应环境变量。"),
            ["settings.keyMissing"] = ("No API key is configured. LangChain4j calls will not run; the backend returns setup guidance instead of real execution.", "尚未配置 API Key。LangChain4j 调用不会执行，后端只会返回配置引导。")
        };

        internal static JavaAgentLanguage CurrentLanguage
        {
            get => (JavaAgentLanguage)EditorPrefs.GetInt(LanguagePrefsKey, (int)JavaAgentLanguage.Chinese);
            set => EditorPrefs.SetInt(LanguagePrefsKey, (int)value);
        }

        internal static string T(string key, params object[] args)
        {
            if (!Text.TryGetValue(key, out var pair))
            {
                return key;
            }

            string value = CurrentLanguage == JavaAgentLanguage.Chinese ? pair.Chinese : pair.English;
            return args == null || args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
