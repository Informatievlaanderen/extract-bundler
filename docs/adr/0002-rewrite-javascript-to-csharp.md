# 2. Rewrite Javascript app to C#

Date: 2023-10-13

## Status

Accepted

## Context

The initial extract-bundler was written in Javascript.
Given the added complexity of `Metadata` and `Download Toepassingen` we need to be able to future-proof this application.

## Decision

We will rewrite the current functionality in the js extract-bundler to C#.

## Consequences

Tested and proven application needs to be rewritten and thus must be completely retested.
