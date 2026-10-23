# Changelog

## [0.1.0] - 2026-10-23

### Added
- Editor window `Tools > BizSim > Localization LLM`: scan, dry run, send, status, fetch, diff review with
  per-row accept, apply.
- `LlmTranslationSettings` asset: collections, source and target locales, game context, do-not-translate
  names, fixed terms, length rules, model, chunk size, state file.
- Missing and changed detection with a sidecar state file (source hash per collection, key id and locale).
- Static API `LlmTranslation` for CLI and MCP runs.
- Uses llm-batch `translate-submit` / `translate-fetch` (checks for placeholders, rich text, Smart String /
  ICU groups, glossary, length and script run there).
