using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Palantyr.Contracts;
using Palantyr.Data.Raven;
using Palantyr.Domain;
using Xunit;

namespace Palantyr.UnitTests;

public sealed class Wave4Tests
{
    private static string CreateTempTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PalantyrWave4Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore lock contention in temp folder
        }
    }

    [Fact]
    public async Task AC_SUM_001_PreGeneration_Privacy_Gatekeeper_Excludes_DmSecrets_In_PartySafe_Mode()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_sum_01";
            string sessionId = "sess_01";

            // 1. Create a public entity (PartyKnown)
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_sunspire",
                CanonicalName = "Sunspire Citadel",
                EntityType = MemoryEntityType.Location,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.CampaignTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                SummaryDescription = "A radiant elven watchtower overlooking the misty valley."
            });

            // 2. Create a DM-only secret entity (DMPrivate)
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_lich_cult",
                CanonicalName = "Cult of the Pale Eye",
                EntityType = MemoryEntityType.Organization,
                Visibility = VisibilityScope.DMPrivate,
                KnowledgeState = KnowledgeState.DMPrivateTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                DmPrivateNotes = "SECRET: Lurking in the catacombs beneath Sunspire Citadel to summon Orcus.",
                SummaryDescription = "A hidden necromantic cult."
            });

            // 3. Create a public entity that has DmPrivateNotes
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_elion",
                CanonicalName = "High Lord Elion",
                EntityType = MemoryEntityType.Character,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.PartyBelief,
                Confidence = ConfidenceLevel.MediumConfidence,
                SummaryDescription = "The noble commander of Sunspire.",
                DmPrivateNotes = "DM CONFIDENTIAL: Elion is secretly blackmailed by the lich cult."
            });

            var summaryService = new SessionSummaryService(store);

            // Generate PartySafe summary
            var partySafeSummary = await summaryService.GenerateSummaryAsync(
                campaignId,
                sessionId,
                new GenerateSummaryRequest(Mode: SummaryVisibilityMode.PartySafe)
            );

            Assert.NotNull(partySafeSummary);
            Assert.Equal(SummaryVisibilityMode.PartySafe, partySafeSummary.Mode);

            // Verify: PartySafe summary contains Sunspire Citadel
            string combinedPartyText = string.Join(" ", 
                partySafeSummary.ExecutiveRecap,
                string.Join(" ", partySafeSummary.KeyEvents.Select(e => e.Description)),
                string.Join(" ", partySafeSummary.MajorDiscoveries.Select(d => d.Description)));

            Assert.Contains("Sunspire Citadel", combinedPartyText);

            // CRITICAL INVARIANT: Zero DM secrets in PartySafe summary
            Assert.DoesNotContain("Cult of the Pale Eye", combinedPartyText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("blackmailed", combinedPartyText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("catacombs", combinedPartyText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Orcus", combinedPartyText, StringComparison.OrdinalIgnoreCase);
            Assert.Null(partySafeSummary.DmPrivateNotes);

            // Generate DmFull summary
            var dmFullSummary = await summaryService.GenerateSummaryAsync(
                campaignId,
                sessionId,
                new GenerateSummaryRequest(Mode: SummaryVisibilityMode.DmFull, IncludeDmPrivateNotes: true)
            );

            Assert.NotNull(dmFullSummary);
            Assert.Equal(SummaryVisibilityMode.DmFull, dmFullSummary.Mode);

            string combinedDmText = string.Join(" ", 
                dmFullSummary.ExecutiveRecap,
                string.Join(" ", dmFullSummary.KeyEvents.Select(e => e.Description)),
                string.Join(" ", dmFullSummary.MajorDiscoveries.Select(d => d.Description)),
                dmFullSummary.DmPrivateNotes ?? "");

            // DmFull summary includes the secret cult or DM notes
            Assert.Contains("Cult of the Pale Eye", combinedDmText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_SUM_002_Summary_Persistence_And_ReadBack_Preserves_Structure()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_sum_02";
            string sessionId = "sess_02";

            var summary = new SessionSummaryDto(
                SummaryId: "sum_02_party",
                CampaignId: campaignId,
                SessionId: sessionId,
                Mode: SummaryVisibilityMode.PartySafe,
                Title: "Session 2: The Whispering Mire",
                ExecutiveRecap: "The party negotiated safe passage through the Whispering Mire.",
                KeyEvents: new List<SummaryKeyEventDto>
                {
                    new("ev_1", "Defeated bog hag", "35:00", "ent_mire", new List<string> { "ent_hag" }, VisibilityScope.PartyKnown)
                },
                MajorDiscoveries: new List<SummaryDiscoveryDto>
                {
                    new("Sunken Shrine of Sehanine", "Ancient shrine submerged in water", KnowledgeState.CampaignTruth, ConfidenceLevel.HighConfidence)
                },
                OutstandingQuestions: new List<string> { "Find key to the inner sanctum" },
                DmPrivateNotes: null,
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                PromptTemplateVersion: "v1.2",
                Sources: new List<ProvenanceRef>()
            );

            await store.SaveSessionSummaryAsync(campaignId, summary);

            var retrieved = await store.GetSessionSummaryAsync(campaignId, sessionId, SummaryVisibilityMode.PartySafe);
            Assert.NotNull(retrieved);
            Assert.Equal("sum_02_party", retrieved.SummaryId);
            Assert.Equal("The party negotiated safe passage through the Whispering Mire.", retrieved.ExecutiveRecap);
            Assert.Single(retrieved.KeyEvents);
            Assert.Equal("Defeated bog hag", retrieved.KeyEvents[0].Description);
            Assert.Single(retrieved.MajorDiscoveries);
            Assert.Equal("Sunken Shrine of Sehanine", retrieved.MajorDiscoveries[0].Subject);

            var list = await store.ListSessionSummariesAsync(campaignId, sessionId);
            Assert.Single(list);
            Assert.Equal(SummaryVisibilityMode.PartySafe, list[0].Mode);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_COR_001_Correction_Propagation_Attaches_DMCorrection_Provenance()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_cor_01";
            string sessionId = "sess_03";

            // Seed entity
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_thief",
                CanonicalName = "Rook",
                EntityType = MemoryEntityType.Character,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.PartyBelief,
                Confidence = ConfidenceLevel.MediumConfidence,
                SummaryDescription = "A common cutpurse encountered in the tavern."
            });

            var summaryService = new SessionSummaryService(store);

            // Generate initial summary
            var initialSummary = await summaryService.GenerateSummaryAsync(
                campaignId,
                sessionId,
                new GenerateSummaryRequest(Mode: SummaryVisibilityMode.DmFull, IncludeDmPrivateNotes: true)
            );

            // DM issues a correction: Rook is actually Captain Valen
            var updateReq = new UpdateSummaryRequest(
                ExecutiveRecap: initialSummary.ExecutiveRecap + " CORRECTION: Rook revealed his identity as Captain Valen.",
                DmCorrectionNote: "Rook is not a common cutpurse; he is Captain Valen in disguise.",
                CorrectedBy: "DM"
            );

            var updatedSummary = await summaryService.PropagateSummaryCorrectionAsync(campaignId, initialSummary.SummaryId, updateReq);
            Assert.NotNull(updatedSummary);
            Assert.Contains("CORRECTION: Rook revealed his identity", updatedSummary.ExecutiveRecap);

            // Verify canonical memory entity was updated with DMCorrection provenance
            var updatedEntity = await store.GetMemoryEntityAsync(campaignId, "ent_thief");
            Assert.NotNull(updatedEntity);
            Assert.Contains("Captain Valen", updatedEntity.DmPrivateNotes);

            // Verify provenance record
            Assert.NotEmpty(updatedEntity.Sources);
            var lastSource = updatedEntity.Sources.Last();
            Assert.Equal(ProvenanceSourceType.DMCorrection, lastSource.SourceType);
            Assert.Contains("Captain Valen", lastSource.NoteOrContext);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_REV_001_Conversational_Needs_Attention_Resolution_Updates_Memory_And_Transcript()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);
            var reviewService = new SessionReviewService(store);

            string campaignId = "camp_rev_01";
            string sessionId = "sess_04";

            // Add an ambiguous pending Needs Attention item
            var attentionItem = new NeedsAttentionItem
            {
                Id = "attn_item_01",
                CampaignId = campaignId,
                SessionId = sessionId,
                SourceType = "ContextAmbiguity",
                Title = "Ambiguous Entity Mention",
                PromptMessageEn = "Did the party find the Chalice of Moradin?",
                PromptMessageEs = "¿Encontró el grupo el Cáliz de Moradín?",
                TargetSpanId = "span_01",
                ApproximateTimestampMs = 12000,
                IsResolved = false
            };
            await store.SaveNeedsAttentionItemAsync(campaignId, attentionItem);

            var pending = await store.ListNeedsAttentionItemsAsync(campaignId, sessionId, unresolvedOnly: true);
            Assert.Single(pending);

            // Resolve the attention item: Confirm as canonical lore
            var resolveReq = new ResolveAttentionItemRequest(
                Action: NeedsAttentionAction.Confirm,
                CorrectionText: "The Sacred Chalice of Moradin is an ancient dwarven artifact.",
                Notes: "Confirmed during review",
                ResolvedBy: "DM"
            );

            bool success = await reviewService.ResolveAttentionItemAsync(campaignId, "attn_item_01", resolveReq);
            Assert.True(success);

            // Verify review item status transitioned to resolved
            var remainingPending = await store.ListNeedsAttentionItemsAsync(campaignId, sessionId, unresolvedOnly: true);
            Assert.Empty(remainingPending);

            // Verify all items shows resolved with decision summary
            var allItems = await store.ListNeedsAttentionItemsAsync(campaignId, sessionId, unresolvedOnly: false);
            Assert.Single(allItems);
            Assert.True(allItems[0].IsResolved);
            Assert.Contains("Confirmed by DM", allItems[0].ResolutionDecision);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_REV_002_Batch_Full_Review_Autosave_And_Undo_Restoration()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);
            var reviewService = new SessionReviewService(store);

            string campaignId = "camp_rev_02";
            string sessionId = "sess_05";

            // Seed two memory entities
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "char_vesper",
                CanonicalName = "Lady Vesper",
                EntityType = MemoryEntityType.Character,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.PartyBelief,
                Confidence = ConfidenceLevel.MediumConfidence,
                SummaryDescription = "A mysterious patron."
            });

            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "loc_iron_gate",
                CanonicalName = "Iron Gate",
                EntityType = MemoryEntityType.Location,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.PartyBelief,
                Confidence = ConfidenceLevel.LowOrUncertain,
                SummaryDescription = "A rumored outpost."
            });

            // Execute Batch Review: Confirm char_vesper, Reject loc_iron_gate
            var batchReq = new BatchReviewRequest(
                SessionId: sessionId,
                Decisions: new List<SingleReviewDecisionDto>
                {
                    new("char_vesper", IsConfirmed: true, IsRejected: false, CorrectionText: "Lady Vesper is a confirmed patron of the guild."),
                    new("loc_iron_gate", IsConfirmed: false, IsRejected: true, RejectionReason: "Fictional campfire rumor, not real location")
                },
                DecidedBy: "DM"
            );

            var undoToken = await reviewService.ProcessBatchReviewAsync(campaignId, batchReq);
            Assert.NotNull(undoToken);
            Assert.Equal(2, undoToken.AffectedEntityIds.Count);

            // Verify updated states in store
            var vesper = await store.GetMemoryEntityAsync(campaignId, "char_vesper");
            Assert.NotNull(vesper);
            Assert.Equal(ConfidenceLevel.DMConfirmed, vesper.Confidence);
            Assert.Equal(KnowledgeState.CampaignTruth, vesper.KnowledgeState);
            Assert.Contains("confirmed patron", vesper.SummaryDescription);

            var ironGate = await store.GetMemoryEntityAsync(campaignId, "loc_iron_gate");
            Assert.NotNull(ironGate);
            Assert.Equal(KnowledgeState.DisprovenOrSupersededBelief, ironGate.KnowledgeState);

            // Now perform UNDO using the snapshot token
            bool undoSuccess = await reviewService.UndoBatchReviewAsync(campaignId, undoToken.UndoTokenId);
            Assert.True(undoSuccess);

            // Verify states reverted back to original
            var restoredVesper = await store.GetMemoryEntityAsync(campaignId, "char_vesper");
            Assert.NotNull(restoredVesper);
            Assert.Equal(KnowledgeState.PartyBelief, restoredVesper.KnowledgeState);
            Assert.Equal("A mysterious patron.", restoredVesper.SummaryDescription);

            var restoredIronGate = await store.GetMemoryEntityAsync(campaignId, "loc_iron_gate");
            Assert.NotNull(restoredIronGate);
            Assert.Equal(KnowledgeState.PartyBelief, restoredIronGate.KnowledgeState);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_001_Grounded_QA_Returns_Exact_Evidence_Citations()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_01";

            // Seed entity with full provenance and first appearance
            var entity = new MemoryEntityDocument
            {
                Id = "ent_black_dragon",
                CanonicalName = "Vorash the Black",
                EntityType = MemoryEntityType.Character,
                Aliases = new List<string> { "The Mire Wyrm", "Acid Wing" },
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.CampaignTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                SummaryDescription = "An ancient adult black dragon dwelling in the Fetid Marsh.",
                FirstAppearance = new FirstAppearanceRef(
                    RealDateUtc: DateTimeOffset.UtcNow.AddDays(-10),
                    CampaignDateText: "3 Eleint, 1492 DR",
                    RelativeTimeContext: "Session 2 - Marsh Crossing",
                    SessionId: "sess_02",
                    LocationEntityId: "ent_fetid_marsh",
                    TimestampMs: 45000
                ),
                DmPrivateNotes = null
            };
            await store.SaveMemoryEntityAsync(campaignId, entity);

            var eonixService = new EonixAssistantService(store);

            var queryReq = new EonixChatQueryRequest(
                Query: "Who is Vorash the Black?",
                Scope: EonixScope.PartyKnowledge
            );

            var response = await eonixService.AskAsync(campaignId, queryReq);
            Assert.NotNull(response);
            Assert.True(response.IsGrounded);
            Assert.Contains("Vorash the Black", response.Answer);
            Assert.Contains("black dragon", response.Answer);

            // Grounding check: Evidence citations must be populated
            Assert.NotEmpty(response.Sources);
            var citation = response.Sources[0];
            Assert.Equal("ent_black_dragon", citation.EntityId);
            Assert.Equal("sess_02", citation.SessionId);
            Assert.Equal(45000, citation.TimestampMs);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_002_Honest_Unknown_Admissions_Zero_Hallucination()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_02";
            var eonixService = new EonixAssistantService(store);

            var queryReq = new EonixChatQueryRequest(
                Query: "What is the secret weakness of the Astral Dreadnought in the Astral Sea?",
                Scope: EonixScope.CampaignDmFull
            );

            var response = await eonixService.AskAsync(campaignId, queryReq);
            Assert.NotNull(response);

            // Invariant FR-EONIX-013: Zero hallucination, admits no record exists
            Assert.False(response.IsGrounded);
            Assert.Empty(response.Sources);
            Assert.Contains("no record", response.Answer, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_003_Scope_Isolation_PartySafe_Hides_Dm_Secrets()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_03";

            // Add an NPC with secret identity
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_mayor",
                CanonicalName = "Mayor Higgins",
                EntityType = MemoryEntityType.Character,
                Visibility = VisibilityScope.PartyKnown,
                KnowledgeState = KnowledgeState.PartyBelief,
                Confidence = ConfidenceLevel.MediumConfidence,
                SummaryDescription = "The genial mayor of Oakhaven town.",
                DmPrivateNotes = "SECRET: Higgins is a doppelganger working for the Shadow Thieves."
            });

            // Add a secret location
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_shadow_lair",
                CanonicalName = "Shadow Thieves Hideout",
                EntityType = MemoryEntityType.Location,
                Visibility = VisibilityScope.DMPrivate,
                KnowledgeState = KnowledgeState.DMPrivateTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                SummaryDescription = "A hidden den beneath the town hall.",
                DmPrivateNotes = "Contains forged ledgers and poison stores."
            });

            var eonixService = new EonixAssistantService(store);

            // Query in PartyKnowledge scope about Mayor Higgins
            var partyResponse = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Tell me about Mayor Higgins",
                Scope: EonixScope.PartyKnowledge
            ));

            Assert.True(partyResponse.IsGrounded);
            Assert.Contains("Mayor Higgins", partyResponse.Answer);
            Assert.Contains("genial mayor", partyResponse.Answer);
            Assert.DoesNotContain("doppelganger", partyResponse.Answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Shadow Thieves", partyResponse.Answer, StringComparison.OrdinalIgnoreCase);

            // Query in PartyKnowledge scope about the secret hideout -> should admit no record
            var secretPartyResponse = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Where is the Shadow Thieves Hideout?",
                Scope: EonixScope.PartyKnowledge
            ));

            Assert.False(secretPartyResponse.IsGrounded);
            Assert.Contains("no record", secretPartyResponse.Answer, StringComparison.OrdinalIgnoreCase);

            // Query in CampaignDmFull scope -> should reveal full notes
            var dmResponse = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Where is the Shadow Thieves Hideout?",
                Scope: EonixScope.CampaignDmFull
            ));

            Assert.True(dmResponse.IsGrounded);
            Assert.Contains("Shadow Thieves Hideout", dmResponse.Answer);
            Assert.Contains("hidden den beneath the town hall", dmResponse.Answer);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_004_Conversational_Teaching_Persists_Memory_With_Provenance()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_04";
            var eonixService = new EonixAssistantService(store);

            var teachReq = new EonixTeachRequest(
                TaughtStatement: "The Silver Key of Kelvor is an ancient elven relic that unlocks the Moonwell gate.",
                TargetEntityName: "The Silver Key of Kelvor",
                IsDmSecret: false,
                TaughtBy: "DM"
            );

            var teachResponse = await eonixService.TeachAsync(campaignId, teachReq);
            Assert.NotNull(teachResponse);
            Assert.True(teachResponse.Success);
            Assert.NotNull(teachResponse.TargetEntityId);

            // Verify persisted in store
            var entity = await store.GetMemoryEntityAsync(campaignId, teachResponse.TargetEntityId);
            Assert.NotNull(entity);
            Assert.Equal("The Silver Key of Kelvor", entity.CanonicalName);
            Assert.Equal(VisibilityScope.PartyKnown, entity.Visibility);
            Assert.Equal(KnowledgeState.CampaignTruth, entity.KnowledgeState);
            Assert.Equal(ConfidenceLevel.DMConfirmed, entity.Confidence);
            Assert.NotEmpty(entity.Sources);
            Assert.Equal(ProvenanceSourceType.DMConversationalTeaching, entity.Sources[0].SourceType);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_SUM_003_Empty_Memory_Boundary_Generates_Clean_Summary()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_sum_empty";
            string sessionId = "sess_empty";

            var summaryService = new SessionSummaryService(store);

            var summary = await summaryService.GenerateSummaryAsync(
                campaignId,
                sessionId,
                new GenerateSummaryRequest(Mode: SummaryVisibilityMode.PartySafe)
            );

            Assert.NotNull(summary);
            Assert.Equal(SummaryVisibilityMode.PartySafe, summary.Mode);
            Assert.NotNull(summary.ExecutiveRecap);
            Assert.Empty(summary.KeyEvents);
            Assert.Empty(summary.MajorDiscoveries);
            Assert.Null(summary.DmPrivateNotes);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_COR_002_Correction_RoundTrip_Preserves_SessionId_And_Collections()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_cor_rt";
            string sessionId = "sess_10";

            var initialSummary = new SessionSummaryDto(
                SummaryId: "sum_sess_10_party",
                CampaignId: campaignId,
                SessionId: sessionId,
                Mode: SummaryVisibilityMode.PartySafe,
                Title: "Session 10: Deep Roads",
                ExecutiveRecap: "The party ventured into the Deep Roads.",
                KeyEvents: new List<SummaryKeyEventDto>
                {
                    new("ev_1", "Traversed the obsidian bridge", "01:15", "loc_bridge", new List<string> { "Party" }, VisibilityScope.PartyKnown)
                },
                MajorDiscoveries: new List<SummaryDiscoveryDto>
                {
                    new("Ancient Dwarven Vault", "Guarded by stone golems", KnowledgeState.CampaignTruth, ConfidenceLevel.HighConfidence)
                },
                OutstandingQuestions: new List<string> { "Where does the mine cart track lead?" },
                DmPrivateNotes: null,
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                PromptTemplateVersion: "v1.2",
                Sources: new List<ProvenanceRef>()
            );

            await store.SaveSessionSummaryAsync(campaignId, initialSummary);

            var summaryService = new SessionSummaryService(store);

            // Apply DM correction only modifying executive recap
            var updateReq = new UpdateSummaryRequest(
                ExecutiveRecap: "The party ventured into the Deep Roads and recovered the ancient dwarven cipher.",
                DmCorrectionNote: "Dwarven cipher was recovered.",
                CorrectedBy: "DM"
            );

            var updated = await summaryService.PropagateSummaryCorrectionAsync(campaignId, "sum_sess_10_party", updateReq);
            Assert.NotNull(updated);

            // Verify SessionId is preserved (not hardcoded to "session-001")
            Assert.Equal(sessionId, updated.SessionId);
            Assert.Equal(SummaryVisibilityMode.PartySafe, updated.Mode);
            Assert.Single(updated.KeyEvents);
            Assert.Equal("Traversed the obsidian bridge", updated.KeyEvents[0].Description);
            Assert.Single(updated.MajorDiscoveries);
            Assert.Equal("Ancient Dwarven Vault", updated.MajorDiscoveries[0].Subject);

            // Verify stored in RavenDB
            var fetched = await store.GetSessionSummaryAsync(campaignId, sessionId, SummaryVisibilityMode.PartySafe);
            Assert.NotNull(fetched);
            Assert.Equal(sessionId, fetched.SessionId);
            Assert.Contains("ancient dwarven cipher", fetched.ExecutiveRecap);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_005_Spanish_Conversational_Teaching_Extracts_Subject()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_es";
            var eonixService = new EonixAssistantService(store);

            var teachReq = new EonixTeachRequest(
                TaughtStatement: "El Bastón de Hielo es un cetro arcano forjado en la era de los gigantes.",
                TargetEntityName: null, // Test automatic extraction with Spanish ' es '
                IsDmSecret: false,
                TaughtBy: "DM"
            );

            var teachResponse = await eonixService.TeachAsync(campaignId, teachReq);
            Assert.NotNull(teachResponse);
            Assert.True(teachResponse.Success);
            Assert.Equal("El Bastón de Hielo", teachResponse.TargetCanonicalName);

            var entity = await store.GetMemoryEntityAsync(campaignId, teachResponse.TargetEntityId);
            Assert.NotNull(entity);
            Assert.Equal("El Bastón de Hielo", entity.CanonicalName);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_REV_003_Invalid_Undo_Token_Returns_False()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);
            var reviewService = new SessionReviewService(store);

            string campaignId = "camp_rev_invalid";
            bool success = await reviewService.UndoBatchReviewAsync(campaignId, "undo_nonexistent_guid_12345");
            Assert.False(success);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task AC_EONIX_006_CharacterScoped_Filters_Unauthorized_Dm_Secrets()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var config = new RavenEmbeddedConfiguration(tempDir);
            var store = new RavenCampaignMemoryStore(config);

            string campaignId = "camp_eonix_char_scope";

            // Add entity with CharacterScoped visibility for "char_thief"
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_thief_guild",
                CanonicalName = "The Silver Daggers",
                EntityType = MemoryEntityType.Organization,
                Visibility = VisibilityScope.CharacterScoped,
                AuthorizedCharacterIds = new List<string> { "char_thief" },
                KnowledgeState = KnowledgeState.CampaignTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                SummaryDescription = "A clandestine thief guild that only rogues know how to contact.",
                DmPrivateNotes = "DM CONFIDENTIAL: Secretly funded by the town mayor."
            });

            // Add secret location (DMPrivate)
            await store.SaveMemoryEntityAsync(campaignId, new MemoryEntityDocument
            {
                Id = "ent_catacombs",
                CanonicalName = "Sunspire Catacombs",
                EntityType = MemoryEntityType.Location,
                Visibility = VisibilityScope.DMPrivate,
                KnowledgeState = KnowledgeState.DMPrivateTruth,
                Confidence = ConfidenceLevel.HighConfidence,
                SummaryDescription = "Forbidden tunnels beneath the cathedral.",
                DmPrivateNotes = "Orcus cult ritual circle."
            });

            var eonixService = new EonixAssistantService(store);

            // Query in CharacterScoped for authorized character "char_thief"
            var authorizedResp = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Who are The Silver Daggers?",
                Scope: EonixScope.CharacterScoped,
                ScopeCharacterId: "char_thief"
            ));

            Assert.True(authorizedResp.IsGrounded);
            Assert.Contains("The Silver Daggers", authorizedResp.Answer);
            // CRITICAL: DM secrets MUST NOT be exposed even in CharacterScoped mode!
            Assert.DoesNotContain("town mayor", authorizedResp.Answer, StringComparison.OrdinalIgnoreCase);

            // Query in CharacterScoped for unauthorized character "char_paladin"
            var unauthorizedResp = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Who are The Silver Daggers?",
                Scope: EonixScope.CharacterScoped,
                ScopeCharacterId: "char_paladin"
            ));

            Assert.False(unauthorizedResp.IsGrounded);
            Assert.Contains("no record", unauthorizedResp.Answer, StringComparison.OrdinalIgnoreCase);

            // Query in CharacterScoped for DMPrivate entity -> Must not leak!
            var privateResp = await eonixService.AskAsync(campaignId, new EonixChatQueryRequest(
                Query: "Tell me about Sunspire Catacombs",
                Scope: EonixScope.CharacterScoped,
                ScopeCharacterId: "char_thief"
            ));

            Assert.False(privateResp.IsGrounded);
            Assert.DoesNotContain("Orcus", privateResp.Answer, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }
}
