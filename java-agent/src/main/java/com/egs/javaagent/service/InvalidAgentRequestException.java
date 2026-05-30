package com.egs.javaagent.service;

public final class InvalidAgentRequestException extends RuntimeException {

    public InvalidAgentRequestException(String message) {
        super(message);
    }
}
