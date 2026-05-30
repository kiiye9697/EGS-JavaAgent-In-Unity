package com.egs.javaagent.service;

public final class ModelProviderSettingsLoader {

    private ModelProviderSettingsLoader() {
    }

    public static ModelProviderSettings loadFromEnvironment() {
        String provider = firstNonBlank(
            System.getenv("EGS_AGENT_PROVIDER"),
            "openai"
        );

        return switch (provider.toLowerCase()) {
            case "glm" -> new ModelProviderSettings(
                "glm",
                firstNonBlank(System.getenv("EGS_AGENT_MODEL"), "glm-4.7"),
                firstNonBlank(System.getenv("GLM_API_KEY"), System.getenv("ZHIPU_API_KEY")),
                firstNonBlank(System.getenv("EGS_AGENT_BASE_URL"), "https://open.bigmodel.cn/api/paas/v4/"),
                firstNonBlank(System.getenv("EGS_AGENT_GATEWAY"), "http")
            );
            case "deepseek" -> new ModelProviderSettings(
                "deepseek",
                firstNonBlank(System.getenv("EGS_AGENT_MODEL"), "deepseek-v4-flash"),
                System.getenv("DEEPSEEK_API_KEY"),
                firstNonBlank(System.getenv("EGS_AGENT_BASE_URL"), "https://api.deepseek.com"),
                firstNonBlank(System.getenv("EGS_AGENT_GATEWAY"), "http")
            );
            default -> new ModelProviderSettings(
                "openai",
                firstNonBlank(System.getenv("EGS_AGENT_MODEL"), "gpt-5"),
                System.getenv("OPENAI_API_KEY"),
                firstNonBlank(System.getenv("EGS_AGENT_BASE_URL"), "https://api.openai.com/v1"),
                firstNonBlank(System.getenv("EGS_AGENT_GATEWAY"), "http")
            );
        };
    }

    private static String firstNonBlank(String first, String fallback) {
        return first == null || first.isBlank() ? fallback : first;
    }
}
