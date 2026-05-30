package com.egs.javaagent.service;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;

public final class ProjectPathResolver {

    public Path safeProjectPath(String projectPath) {
        if (projectPath == null || projectPath.isBlank()) {
            return null;
        }

        Path path = Path.of(projectPath).normalize();
        return Files.exists(path) && Files.isDirectory(path) ? path : null;
    }

    public Path resolveTarget(Path projectPath, String target) {
        if (projectPath == null || target == null || target.isBlank()) {
            return null;
        }

        String normalizedTarget = target.replace('\\', '/');
        Path directPath = Path.of(normalizedTarget);
        if (directPath.isAbsolute()) {
            Path absolute = directPath.normalize();
            return isWithin(projectPath, absolute) ? absolute : null;
        }

        List<Path> candidates = candidatePaths(projectPath, normalizedTarget);
        for (Path candidate : candidates) {
            if (Files.exists(candidate)) {
                return candidate;
            }
        }

        return candidates.isEmpty() ? null : candidates.get(0);
    }

    private List<Path> candidatePaths(Path projectPath, String normalizedTarget) {
        LinkedHashSet<Path> candidates = new LinkedHashSet<>();
        boolean projectIsAssetsDirectory = isAssetsDirectory(projectPath);

        if (normalizedTarget.startsWith("Assets/")) {
            String relativeUnderAssets = normalizedTarget.substring("Assets/".length());
            if (projectIsAssetsDirectory) {
                candidates.add(projectPath.resolve(relativeUnderAssets).normalize());
            } else {
                candidates.add(projectPath.resolve("Assets").resolve(relativeUnderAssets).normalize());
            }

            candidates.add(projectPath.resolve("unity-client").resolve("Assets").resolve(relativeUnderAssets).normalize());
        }

        candidates.add(projectPath.resolve(normalizedTarget).normalize());

        if (!projectIsAssetsDirectory) {
            candidates.add(projectPath.resolve("unity-client").resolve(normalizedTarget).normalize());
        }

        return new ArrayList<>(candidates);
    }

    private boolean isAssetsDirectory(Path projectPath) {
        Path fileName = projectPath.getFileName();
        return fileName != null && "assets".equals(fileName.toString().toLowerCase(Locale.ROOT));
    }

    private boolean isWithin(Path root, Path candidate) {
        Path normalizedRoot = root.normalize();
        Path normalizedCandidate = candidate.normalize();
        return normalizedCandidate.startsWith(normalizedRoot)
            || normalizedCandidate.startsWith(normalizedRoot.resolve("unity-client").normalize());
    }

    public Path resolveWritableTarget(Path projectPath, String target) {
        if (projectPath == null || target == null || target.isBlank()) {
            return null;
        }

        String normalizedTarget = target.replace('\\', '/');
        Path directPath = Path.of(normalizedTarget);
        if (directPath.isAbsolute()) {
            Path absolute = directPath.normalize();
            return isWithin(projectPath, absolute) ? absolute : null;
        }

        for (Path candidate : candidatePaths(projectPath, normalizedTarget)) {
            if (isWithin(projectPath, candidate)) {
                return candidate;
            }
        }

        return null;
    }
}
