# BizSim Localization LLM

Editor tool that fills and refreshes Unity Localization string tables through the studio
**llm-batch** tool (`D:/Projects/studio-ops/shared-tools/llm-batch`, Anthropic Message Batches,
half price, asynchronous). Editor-only; nothing ships in a player build.

## What it does

1. **Scan**: for every configured String Table Collection and target locale, finds entries that are
   *missing* (no entry or empty) or *changed* (the source text differs from the one recorded when the
   translation was written).
2. **Dry run**: writes `JOB.json` and prints llm-batch's request count and cost estimate. Nothing is sent.
3. **Send**: submits one batch. Each key goes only to the locales where it is missing or changed.
4. **Status / Fetch**: when the batch has ended, llm-batch checks every answer (placeholders, rich text,
   Smart String / ICU groups, glossary, length, script) and writes `RESULTS.json`.
5. **Review and Apply**: the window lists old and new text per locale with the check results. Only rows
   that passed and are ticked are written (with Undo). The sidecar state records the source text each
   translation was made from.

No API key exists in this package or in the project: llm-batch reads its own key store.

## Setup

1. Add the package (Package Manager, git URL or a local path).
2. `Tools > BizSim > Localization LLM` > **Create settings** (`Assets/Editor/BizSimLocalizationLlmSettings.asset`).
3. In the settings asset: the collections, the source locale, target locales (empty = all), one paragraph of
   game context, do-not-translate names (game and brand names), fixed terms per locale, length rules
   (per key prefix: max characters or a ratio to the source), model (cheapest first) and chunk size.
4. In the window: the llm-batch path and the Python executable (per machine, stored in EditorPrefs).
5. Once, on a project that already has translations: **Record baseline**, so existing human or earlier
   translations count as current and later source edits are detected.

`stateFile` (default `docs/translations/llm-translation-state.json`) belongs in the repo; `workDir`
(default `Library/LocalizationLlm`) is scratch that must survive an Editor restart between Send and
Fetch, which is why it is not `Temp/` (Unity wipes `Temp/` on close).

## CLI / automation

Every step is a static method on `BizSim.Unity.Localization.Llm.Editor.LlmTranslation`, so it runs from the
Unity CLI (`run_script` / `eval`) or the Unity MCP without the window:

| Call | Returns |
|---|---|
| `Scan()` | counts per locale, missing + changed |
| `Submit(false)` / `Submit(true)` | the estimate / the submitted batch id |
| `Status()` | batch progress |
| `Fetch()` | rows with check results (also saved as RESULTS.json) |
| `ApplyAllPassing()` | writes every row that passed its checks |
| `RecordBaseline()` | stamps existing translations as current |

If the project keeps its translations in a CSV (Unity's CSV extension), export the collection after Apply.

## Cost

The dry run prints the estimate before anything is sent; fetch prints the actual tokens and USD. Log
every send in the studio cost ledger (`studio-ops/shared-tools/costs`). Reference: Junkyard Tycoon,
1893 keys x 29 locales full retranslation about 0.25 USD on Haiku with the batch discount.
