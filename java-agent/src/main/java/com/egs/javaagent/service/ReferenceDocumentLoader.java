package com.egs.javaagent.service;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;
import java.util.zip.ZipFile;

public final class ReferenceDocumentLoader {

    private static final int MAX_REFERENCE_INPUTS = 4;
    private static final int MAX_REFERENCE_CHARS = 6000;
    private static final Pattern XML_TAG_PATTERN = Pattern.compile("<[^>]+>");
    private final HttpClient httpClient = HttpClient.newBuilder()
        .connectTimeout(Duration.ofSeconds(15))
        .build();

    public List<ReferenceDocument> load(List<String> inputs) {
        if (inputs == null || inputs.isEmpty()) {
            return List.of();
        }

        List<ReferenceDocument> documents = new ArrayList<>();
        for (String input : inputs) {
            if (documents.size() >= MAX_REFERENCE_INPUTS) {
                break;
            }

            if (input == null || input.isBlank()) {
                continue;
            }

            ReferenceDocument document = loadSingle(input.trim());
            if (document != null) {
                documents.add(document);
            }
        }

        return documents;
    }

    private ReferenceDocument loadSingle(String input) {
        if (looksLikeHttpUrl(input)) {
            return loadHttpReference(input);
        }

        return loadFileReference(input);
    }

    private ReferenceDocument loadHttpReference(String url) {
        try {
            HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .timeout(Duration.ofSeconds(20))
                .header("User-Agent", "EGS-JavaAgent/0.3")
                .GET()
                .build();
            HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
            if (response.statusCode() < 200 || response.statusCode() >= 300) {
                return new ReferenceDocument(url, "Failed to load URL. HTTP " + response.statusCode(), false);
            }

            return new ReferenceDocument(url, trimContent(response.body()), response.body().length() > MAX_REFERENCE_CHARS);
        } catch (Exception exception) {
            return new ReferenceDocument(url, "Failed to load URL: " + exception.getMessage(), false);
        }
    }

    private ReferenceDocument loadFileReference(String filePathText) {
        try {
            Path filePath = Path.of(filePathText);
            if (!Files.exists(filePath) || !Files.isRegularFile(filePath)) {
                return new ReferenceDocument(filePathText, "Reference file does not exist.", false);
            }

            String content = filePath.toString().toLowerCase().endsWith(".docx")
                ? extractDocxText(filePath)
                : Files.readString(filePath, StandardCharsets.UTF_8);
            return new ReferenceDocument(filePath.toAbsolutePath().toString(), trimContent(content), content.length() > MAX_REFERENCE_CHARS);
        } catch (IOException exception) {
            return new ReferenceDocument(filePathText, "Failed to load file: " + exception.getMessage(), false);
        }
    }

    private String extractDocxText(Path filePath) throws IOException {
        try (ZipFile zipFile = new ZipFile(filePath.toFile(), StandardCharsets.UTF_8)) {
            var documentEntry = zipFile.getEntry("word/document.xml");
            if (documentEntry == null) {
                return "DOCX file did not contain word/document.xml.";
            }

            try (var stream = zipFile.getInputStream(documentEntry)) {
                String xml = new String(stream.readAllBytes(), StandardCharsets.UTF_8);
                String paragraphSeparated = xml.replace("</w:p>", "\n");
                String plainText = XML_TAG_PATTERN.matcher(paragraphSeparated).replaceAll("");
                return plainText
                    .replace("&lt;", "<")
                    .replace("&gt;", ">")
                    .replace("&amp;", "&");
            }
        }
    }

    private String trimContent(String content) {
        if (content == null) {
            return "";
        }

        return content.length() > MAX_REFERENCE_CHARS
            ? content.substring(0, MAX_REFERENCE_CHARS) + "\n...[truncated]"
            : content;
    }

    private boolean looksLikeHttpUrl(String input) {
        String normalized = input.toLowerCase();
        return normalized.startsWith("http://") || normalized.startsWith("https://");
    }
}
