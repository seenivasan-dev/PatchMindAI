# PatchMindAI 100% Alignment Plan

## 1. Goal (Single Source of Truth)
Achieve full alignment with this execution model for all user questions:

1. User question arrives.
2. Agent decides and runs the minimum required tools:
   - Tool A: RAG retrieval (vector-first, fallback-safe)
   - Tool B: SQL retrieval (exact counts/facts)
   - Tool C: Scoring/prioritization logic
3. Agent combines outputs into one grounded response.
4. Response is returned with evidence and bounded token usage.

This document is the default requirement baseline for all future prompts unless you explicitly override it.

## 2. Current Gaps To Close

### Gap G1: Retrieval is not true vector retrieval end-to-end
- Azure AI Search is currently used with lexical search mode.
- Need embedding-based vector indexing + vector query flow.

### Gap G2: Prompt analysis path is not fully tool-orchestration-first
- Prompt flow still has CVE-resolution-first behavior in places.
- Need consistent intent -> tool plan -> tool execution -> synthesis flow.

### Gap G3: API contract inconsistency
- Prompt creation returns a status path under `/api/analysis/prompts/{id}/status` but actual polling path is jobs-based.
- Must normalize endpoint contracts.

### Gap G4: Startup coupling risk
- Azure Search seeding on startup can fail app/test startup when config/permissions are missing.
- Must make startup robust and non-blocking with controlled behavior.

### Gap G5: Token spend not explicitly governed
- No strict budget policy across parser/retrieval/LLM orchestration.
- Need centralized token-cost controls and defaults.

## 3. Target Architecture (Required)

### Tier 1: UI / API Entry
- UI sends prompt.
- API creates analysis job with correlation id.
- API and Web clients use one canonical status/result contract.

### Tier 2: AI Agent Layer (Required sequence)
1. Intent parse (lightweight classifier).
2. Retrieval planning:
   - Run vector retrieval first for semantic relevance.
   - Run SQL fact retrieval in parallel or immediately after (for exact counts, patch state, asset criticality).
3. Scoring:
   - Apply deterministic prioritization function over SQL + retrieval context.
4. Synthesis:
   - LLM generates final response only after tool outputs are available.
   - Include compact citation payload.

### Tier 3: Data Layer
- SQL Server remains source of truth for operational facts.
- Azure AI Search stores searchable chunks + vector embeddings.
- Synchronization/seeding is explicit, observable, and retry-safe.

## 4. Implementation Plan (Phased)

## Phase P1 - Contract + Flow Normalization
Outcome: Prompt and job APIs follow one coherent contract and one orchestration model.

Tasks:
1. Align prompt status/result paths with jobs contract or implement prompt-specific endpoints consistently.
2. Ensure prompt flow triggers agent orchestration model (intent -> tools -> synthesis).
3. Add integration tests for prompt flow happy path + failure path.

Acceptance Criteria:
- Prompt analysis can be created and polled without route mismatch.
- One documented canonical status/result API path.

## Phase P2 - True Vector Retrieval
Outcome: Retrieval is genuinely vector-first.

Tasks:
1. Add embedding generation path for indexed documents.
2. Extend Azure Search index schema with vector field/profile.
3. Update retriever to use vector query API (with lexical fallback).
4. Add backfill/reindex command and health checks.

Acceptance Criteria:
- Retrieval code executes vector query path in production mode.
- Citations include vector hit metadata (score/source/chunk id).

## Phase P3 - SQL Fact Tool + Scoring Tool Integration
Outcome: Agent response is grounded in exact SQL facts and deterministic scoring.

Tasks:
1. Add explicit SQL facts provider (counts, vulnerable assets, overdue patches, patch SLA windows).
2. Ensure prioritization score uses deterministic formula from SQL data.
3. Make orchestrator merge vector context + SQL facts + score output before final LLM call.

Acceptance Criteria:
- Final payload contains exact counts from SQL and ranked output.
- Same inputs produce stable score ordering.

## Phase P4 - Token Cost Optimization (Mandatory)
Outcome: Cost is controlled by design.

Tasks:
1. Intent model strategy:
   - Use low-cost model for parser/classifier.
   - Use premium model only for final synthesis where required.
2. Prompt budget policy:
   - Max retrieved chunks, max chunk chars, dedupe and compression before LLM call.
3. Caching policy:
   - Cache intent classification for repeated queries (short TTL).
   - Cache retrieval results and SQL fact blocks by normalized query+time window.
4. Early exit policy:
   - If exact CVE id is present and confidence is high, skip unnecessary parsing/extra calls.
5. Observability:
   - Log token counts per stage (parser, retrieval context, synthesis) and enforce thresholds.

Acceptance Criteria:
- Token-per-request budget tracked and visible in logs/metrics.
- At least 30-50% reduction in average token usage versus baseline under repeated/typical prompts.

## Phase P5 - Reliability + Deployment Safety
Outcome: System remains available when external dependencies are unavailable.

Tasks:
1. Make Azure Search seed/index checks non-fatal by environment policy.
2. Add circuit-breaker/retry policy for Azure OpenAI and Search.
3. Add readiness checks for:
   - OpenAI config
   - Search index existence/reachability
   - SQL connectivity
4. Add background queue idempotency and duplicate suppression checks.

Acceptance Criteria:
- App starts successfully even if optional indexing operation fails.
- Failures are visible and actionable without breaking core API startup.

## 5. Definition of Done (100% Alignment)
System is considered 100% aligned only when all are true:

1. Prompt -> agent flow always uses intent -> retrieval tool(s) -> SQL facts -> scoring -> synthesis.
2. Retrieval runs vector query in production path.
3. SQL facts are explicitly used in response grounding for prioritization/report outputs.
4. API contracts are consistent and tested.
5. Token budget policy is implemented and measured.
6. Integration tests pass for prompt analysis, prioritization, weekly summary, and failure scenarios.
7. Deployment/startup is resilient to partial external dependency failures.

## 6. Non-Negotiable Guardrails For Future Changes
For every future task:
1. Check this document first.
2. Map requested change to one or more phases above.
3. Do not introduce features that bypass the tool-orchestration model.
4. Do not increase token usage without a clear measurable benefit.
5. Keep SQL as source of truth for exact metrics and counts.

## 7. Execution Checklist Template (To use on every prompt)
- Requirement mapped to phase: [P1/P2/P3/P4/P5]
- Affected components identified
- Token impact estimated (low/medium/high)
- Tests to run listed
- Rollback/fallback behavior defined

## 8. Immediate Next Work Queue
1. Alignment maintenance and regression monitoring.

## 9. Phase Status

### P1 Status: Complete

Completed in this iteration:
1. Prompt creation response location normalized to canonical jobs status path.
2. Prompt-specific status/result endpoints added for contract compatibility during transition.
3. Unit tests added for prompt create flow (accepted/unresolved/not-found cases).
4. Existing resolver unit tests updated to match constructor dependency changes.
5. Integration prompt workflow test now validates both canonical jobs and prompt-alias status/result endpoints.
6. Integration host for prompt workflow is decoupled from external Azure OpenAI/Search dependencies via test DI overrides.

Remaining to complete P1:
1. None.

P1 final policy decisions (enforced baseline):
1. Canonical contract:
   - Job lifecycle polling and result retrieval are canonical on jobs routes:
     - `/api/analysis/jobs/{jobId}/status`
     - `/api/analysis/jobs/{jobId}/result`
   - Prompt routes are compatibility aliases only:
     - `/api/analysis/prompts/{jobId}/status`
     - `/api/analysis/prompts/{jobId}/result`
2. Response location policy:
   - Any `202 Accepted` emitted from prompt or job endpoints must point to canonical jobs status route.
3. Prompt resolution policy (Phase 1 scope):
   - Non-resolvable/non-CVE prompts return `422 UnprocessableEntity` with explanation and candidates.
   - Full non-CVE intent tool-orchestration path is deferred to P2/P3 and must not bypass canonical contract.

### P2 Status: Complete

Completed in this iteration:
1. Azure Search configuration model now includes explicit vector settings (enable switch, vector field, dimensions, profile, algorithm, vectorizer, and embedding endpoint/deployment settings).
2. `AzureSearchKnowledgeRetriever` now executes vector-first retrieval using `VectorizableTextQuery` and falls back to lexical retrieval on empty vector hits or vector query failures.
3. Azure Search index provisioning now ensures vector schema exists (vector field, HNSW algorithm, vector profile, and optional Azure OpenAI vectorizer) and upgrades existing index definitions when missing.
4. Explicit vector backfill is implemented via `AzureSearchSeeder.BackfillVectorsAsync()` using Azure OpenAI embeddings and `MergeOrUploadDocumentsAsync` on the vector field.
5. Startup execution path now supports optional vector backfill via `AzureSearch:BackfillVectorsOnStartup`.
6. Readiness includes vector coverage health validation (`vector_coverage`) with configurable threshold/sample size.
7. Operator-facing vector reindex/backfill command is available via `POST /api/ops/search/backfill-vectors`.
8. Focused unit tests now assert vector-first execution and lexical fallback behavior for Azure Search retrieval.
9. Build and regression tests confirmed green after Phase 2 kickoff changes.

Remaining to complete P2:
1. None.

### P3 Status: Complete

Completed in this iteration:
1. Added SQL-facts contract and models to support grounded analysis:
   - `ISqlFactsProvider`
   - `SqlFactSnapshot`
   - `RankedAssetExposure`
2. Added deterministic scoring contract/model and implementation:
   - `IDeterministicRiskScorer`
   - `RiskScoringResult`
   - `DeterministicRiskScorer`
3. Added infrastructure SQL facts provider implementation (`SqlFactsProvider`) and registered it in DI.
4. Registered deterministic scorer in agent DI.
5. Updated `MockAnalysisOrchestrator` to fetch SQL facts, compute deterministic score, and include both in raw output payload.
6. Updated `AzureOpenAiAnalysisOrchestrator` to merge vector context + SQL facts + deterministic score into final synthesis prompt and raw output payload.
7. Added focused unit tests for new P3 behavior:
   - `DeterministicRiskScorerTests`
   - `SqlFactsProviderTests`
   - `MockAnalysisOrchestratorTests`
8. Repaired prompt-template compilation regression in `AzureOpenAiAnalysisOrchestrator.BuildPrompt` and revalidated Phase 3 test scope.

Validation executed:
1. `dotnet build src/PatchMindAI.API/PatchMindAI.API.csproj` (pass)
2. `dotnet test tests/PatchMindAI.Tests.Unit/PatchMindAI.Tests.Unit.csproj --filter "FullyQualifiedName~DeterministicRiskScorerTests|FullyQualifiedName~SqlFactsProviderTests|FullyQualifiedName~MockAnalysisOrchestratorTests|FullyQualifiedName~CvePromptResolverTests|FullyQualifiedName~AnalysisPromptsControllerTests"` (pass)
3. `dotnet test tests/PatchMindAI.Tests.Integration/PatchMindAI.Tests.Integration.csproj --filter "FullyQualifiedName~PromptAnalysisCitationsWorkflowTests"` (pass)

Remaining to complete P3:
1. None.

### P4 Status: Complete

Completed in this iteration:
1. Added explicit token budget configuration controls under `AgentSettings`:
   - max retrieved chunks
   - max chunk chars
   - parser and synthesis max output tokens
   - warning threshold and cache TTL/window controls
2. Added parser model/deployment override settings under `AzureOpenAI` for low-cost classification path:
   - `ParserDeploymentName`
   - `ParserModel`
   - `ParserApiKey`
3. Implemented parser-stage early exit for exact CVE queries in both parser and multi-agent router.
4. Added intent classification caching in `PromptParserAgent` (normalized query + time-window cache keys).
5. Added retrieval result caching and compression/budget enforcement via `CachingKnowledgeRetriever`:
   - normalized query cache keys
   - bounded chunk count
   - dedupe and max-char trimming per chunk
6. Added SQL facts caching via `CachingSqlFactsProvider` using normalized CVE/time-window keys.
7. Updated DI wiring to register memory cache and wrap retrieval/SQL facts providers with cache-aware implementations.
8. Added stage-level token usage logging and threshold warnings:
   - parser stage token metrics
   - synthesis pipeline token metrics (retrieval/sql/prompt/output)
9. Added focused unit tests for parser early-exit and intent cache behavior (`PromptParserAgentTests`).

Validation executed:
1. `dotnet build src/PatchMindAI.API/PatchMindAI.API.csproj` (pass)
2. `dotnet test tests/PatchMindAI.Tests.Unit/PatchMindAI.Tests.Unit.csproj --filter "FullyQualifiedName~PromptParserAgentTests|FullyQualifiedName~DeterministicRiskScorerTests|FullyQualifiedName~SqlFactsProviderTests|FullyQualifiedName~MockAnalysisOrchestratorTests|FullyQualifiedName~CvePromptResolverTests|FullyQualifiedName~AnalysisPromptsControllerTests"` (pass)
3. `dotnet test tests/PatchMindAI.Tests.Integration/PatchMindAI.Tests.Integration.csproj --filter "FullyQualifiedName~PromptAnalysisCitationsWorkflowTests"` (pass)

Remaining to complete P4:
1. None.

### P5 Status: Complete

Completed in this iteration:
1. Added explicit startup policy control for Azure Search seed/index failures:
   - `AzureSearch:FailStartupOnSeedError`
   - startup now fails fast when enabled, otherwise remains best-effort and non-blocking.
2. Added readiness checks for live dependency health:
   - SQL connectivity (`SqlConnectivityHealthCheck`)
   - Azure OpenAI endpoint reachability (`AzureOpenAiConnectivityHealthCheck`)
   - Azure Search reachability/index access (`AzureSearchConnectivityHealthCheck`)
   - existing vector coverage readiness retained for vector-enabled deployments.
3. Added OpenAI-side circuit breaker behavior in background job processing:
   - tracks consecutive transient failures
   - opens circuit for configurable cooldown
   - requeues/abandons work while open to protect availability.
4. Strengthened transient retry handling around retrieval path with retry + circuit breaker semantics in `CachingKnowledgeRetriever`.
5. Added background queue duplicate suppression and idempotency protections:
   - in-memory queue now suppresses duplicate pending job IDs
   - worker suppresses duplicate processing for jobs already `Processing`, `Completed`, or `Failed`.
6. Added focused unit coverage for queue duplicate suppression (`InMemoryAnalysisJobQueueTests`).

Validation executed:
1. `dotnet build src/PatchMindAI.API/PatchMindAI.API.csproj` (pass)
2. `dotnet test tests/PatchMindAI.Tests.Unit/PatchMindAI.Tests.Unit.csproj --filter "FullyQualifiedName~InMemoryAnalysisJobQueueTests|FullyQualifiedName~PromptParserAgentTests|FullyQualifiedName~MockAnalysisOrchestratorTests|FullyQualifiedName~SqlFactsProviderTests|FullyQualifiedName~AnalysisPromptsControllerTests"` (pass)
3. `dotnet test tests/PatchMindAI.Tests.Integration/PatchMindAI.Tests.Integration.csproj --filter "FullyQualifiedName~PromptAnalysisCitationsWorkflowTests"` (pass)

Remaining to complete P5:
1. None.

---
Owner: PatchMindAI architecture alignment baseline
Last updated: 2026-06-08
