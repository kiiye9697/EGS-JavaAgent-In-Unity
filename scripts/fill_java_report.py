import glob
import os
import shutil
import zipfile
from xml.etree import ElementTree as ET


NS = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
P_TAG = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}p"
XML_SPACE = "{http://www.w3.org/XML/1998/namespace}space"


BODY_TEXT = {
    21: "基于 Java 与 LangChain4j 的 Unity 智能辅助开发插件设计与实现",
    23: "本课程设计面向 Unity 游戏开发中的重复性、跨文件和高风险修改任务，目标是实现一个基于 Java 的智能辅助开发插件。系统在不改变 Unity 现有工作流的前提下，把大模型推理、项目检索、文件修改建议、编译反馈和人工审批串成可控闭环，使开发者可以用自然语言完成脚本、着色器、材质和场景相关任务，同时保留撤销和复核能力。",
    24: "项目的核心目标不是替代 Unity 编辑器，而是把“读取上下文、生成提案、人工确认、执行修改、编译验证、失败回滚”组织成统一流程。这样既能提高课程设计和原型开发效率，也便于在演示和验收时展示完整的软件工程闭环。",
    27: "系统设计参考了 ReAct 和 Toolformer 的工具调用思想。ReAct 强调“推理 + 行动”的交替过程，适合把读取项目、分析上下文和生成修改建议组织成可追踪流程；Toolformer 提供了工具调用可以被纳入模型工作流的思路，使模型不只是回答问题，而是可以主动使用文件读取、目录探索和参考材料检索等能力。",
    28: "进一步地，系统也吸收了 Reflexion 和 SWE-agent 的思路。Reflexion 对应编译错误驱动的修复迭代，适合做自动 debug；SWE-agent 则说明语言模型可以结合文件操作、验证和反馈完成较完整的软件工程任务闭环。LangChain4j 作为 Java 侧编排框架，负责把模型、工具、记忆和响应结构连接起来。",
    31: "实现过程中主要使用 Java、LangChain4j、Unity Editor 脚本、HTTP/JSON 通信以及本地 JDK 打包部署方案。Unity 端负责界面、审批、调试和场景感知，Java 端负责代理编排、工具调用、文件写入和结果封装；两端通过本地接口对接，使插件能在目标 Unity 工程中直接启动嵌入式 Java 服务。",
    32: "项目还结合了本地参考文档、能力目录和部署脚本，支持把 `unity-client/Assets/EGS/JavaAgent` 复制到目标 Unity 工程中，并携带嵌入式 JDK 与 Java 服务一起运行。这样即使不依赖外部 MCP 环境，系统也能以本地工具和工作流编排的方式完成基本的智能辅助开发任务。",
    36: "系统采用“Unity 工作台 + Java 代理服务 + 本地嵌入式运行时”的三层结构。Unity 端提供可停靠窗口、审批队列、调试控制台、节点式流程展示和回滚历史；Java 端负责读取项目文件、参考材料和编译状态，并通过 LangChain4j 调用模型生成建议。整体流程强调先读后写、先提案后执行、先审批后落盘，从而降低模型直接修改工程带来的不可控风险。",
    38: "核心能力按 Shader、Material、Function、Validation、Scene、Project 六组组织，并配合内置技能配置完成任务路由。Shader 和 Material 侧重着色器、材质与 NPR 效果相关需求；Function 侧重脚本、组件和工具类实现；Validation 侧重编译诊断和自动修复；Scene 侧重当前场景、选中对象和挂载目标；Project 侧重文件检索、参考材料导入和整体工程探索。",
    41: "在实际 Unity 工程中，插件能够正常启动本地 Java 服务，并在 `http://localhost:8765/health` 返回健康状态。Unity 端可以打开可停靠的 Java Agent 窗口，查看当前请求、审批建议、编译诊断和执行日志；在接收到脚本修改请求后，系统可以先读取项目上下文，再生成可审核的建议文件或替换内容，用户批准后再执行写入。",
    42: "对于编译报错场景，系统能够读取编译消息并进入修复流程。当前实现还支持 proposal diff、批量审批、安全创建自动批准、实现后聚焦目标对象和回滚机制，因此能够把“提出修改”和“验证修改”放在同一个工作台里完成。",
    45: "实验表明，该系统的优势不在于直接替代人工写代码，而在于把 Unity 编辑器中的高频操作结构化。一方面，它比纯聊天式助手更能看到场景、选中对象和工程状态；另一方面，它比直接让模型改工程更安全，因为修改前后都经过审批、差异预览和回滚保护。",
    46: "当前版本的不足主要在于部分能力仍以文件级修改为主，复杂材质图、节点网络和更细粒度补丁还可以继续加强。后续如果补充更完整的 Shader/Material 解析、节点式编辑和自动测试，系统会更接近真正的 Unity 智能开发工作台。",
    49: "本课程设计完成了一个 Java + LangChain4j 驱动的 Unity 智能辅助开发插件原型，实现了项目读取、参考资料注入、能力分组、审批执行、编译修复和回滚管理等关键流程。这个原型已经具备课程设计展示所需的主体功能，也能作为后续扩展的基础。",
    50: "后续可以继续扩展 MCP 兼容层、更细的 Shader/Material 处理能力、代码片段级补丁、自动化测试和更丰富的可视化节点界面，使其更接近成熟的 Unity 智能开发工作台。若作为毕业设计或课程大作业继续推进，建议重点完善测试样例、界面截图和典型交互演示。",
    54: "[1] Yao S, et al. ReAct: Synergizing Reasoning and Acting in Language Models. arXiv:2210.03629. [2] Schick T, et al. Toolformer: Language Models Can Teach Themselves to Use Tools. arXiv:2302.04761.",
    55: "[3] Shinn N, et al. Reflexion: Language Agents with Verbal Reinforcement Learning. arXiv:2303.11366. [4] Yang J, et al. SWE-agent: Agent-Computer Interfaces Enable Automated Software Engineering. arXiv:2405.15793.",
    56: "[5] LangChain4j Project. https://github.com/langchain4j/langchain4j. [6] 本项目参考文档：`docs/capability-catalog.md`、`docs/approval-architecture.md`、`docs/reference-architecture.md`、`docs/unity-deployment.md`。",
    58: "附录A：关键文件包括 Java 代理服务、Unity 编辑器窗口、审批队列、调试控制台、编译诊断跟踪和部署脚本。",
    59: "附录B：部署方式为将 `unity-client/Assets/EGS/JavaAgent` 复制到目标 Unity 工程的 `Assets/EGS/JavaAgent` 目录，并同时携带嵌入式 JDK 和 Java 服务。",
    60: "附录C：项目的能力分组可概括为 Shader、Material、Function、Validation、Scene、Project 六类，便于在不同任务场景下选择合适的工具路径。",
    61: "附录D：界面截图、运行结果和编译日志可在后续验收时补充，当前版本保留为可编辑草稿。",
    62: "附录E：如果需要继续扩展，可以在此基础上补充代码清单、测试样例和更详细的流程图。",
    63: "附录F：本报告正文旨在作为课程设计初稿，便于用户根据实际演示结果继续修改完善。",
}


def find_template(downloads: str) -> str:
    candidates = glob.glob(os.path.join(downloads, "*模板*.docx"))
    if not candidates:
        candidates = glob.glob(os.path.join(downloads, "*.docx"))
    if not candidates:
        raise FileNotFoundError("No .docx template found in Downloads")
    return max(candidates, key=os.path.getmtime)


def set_paragraph_text(paragraph, text: str) -> None:
    for child in list(paragraph):
        if child.tag != "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}pPr":
            paragraph.remove(child)
    run = ET.SubElement(paragraph, "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}r")
    text_node = ET.SubElement(run, "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t")
    if text.startswith(" ") or text.endswith(" ") or "\n" in text:
        text_node.set(XML_SPACE, "preserve")
    text_node.text = text


def fill_docx(src_path: str, dst_path: str) -> None:
    with zipfile.ZipFile(src_path, "r") as zin:
        entries = {info.filename: zin.read(info.filename) for info in zin.infolist()}
        infos = zin.infolist()

    root = ET.fromstring(entries["word/document.xml"])
    body = root.find("w:body", NS)
    paragraphs = [child for child in list(body) if child.tag == P_TAG]

    for index, text in BODY_TEXT.items():
        if index < len(paragraphs):
            set_paragraph_text(paragraphs[index], text)

    entries["word/document.xml"] = ET.tostring(root, encoding="utf-8", xml_declaration=True)

    with zipfile.ZipFile(dst_path, "w", compression=zipfile.ZIP_DEFLATED) as zout:
        for info in infos:
            zout.writestr(info, entries[info.filename])


def main() -> None:
    downloads = os.path.join(os.environ["USERPROFILE"], "Downloads")
    src = find_template(downloads)
    dst = os.path.join(os.path.dirname(os.path.dirname(__file__)), "Java大作业模板_已填写.docx")
    shutil.copyfile(src, dst)
    fill_docx(src, dst)
    print(dst)


if __name__ == "__main__":
    main()
