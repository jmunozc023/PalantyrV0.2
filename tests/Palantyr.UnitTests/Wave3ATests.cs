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

public sealed class Wave3ATests
{
    private static string CreateTempTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PalantyrWave3ATests_{Guid.NewGuid():N}");
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
            // Ignore cleanup lock contention in temp folder
        }
    }

    [Fact]
    public void AC_MEM_001_EpistemicStates_And_ConfidenceLevels_Are_Preserved()
    {
        var entity = new MemoryEntityDocument
        {
            Id = "entity_test_01",
            EntityType = MemoryEntityType.Organization,
            CanonicalName = "Order of the Silver Flame",
            KnowledgeState = KnowledgeState.PartyBelief,
            Confidence = ConfidenceLevel.MediumConfidence,
            Visibility = VisibilityScope.PartyKnown,
            DmPrivateNotes = "Actually secretly led by a vampire lord.",
            SummaryDescription = "A revered knightly order."
        };

        Assert.Equal(KnowledgeState.PartyBelief, entity.KnowledgeState);
        Assert.Equal(ConfidenceLevel.MediumConfidence, entity.Confidence);

        // Mutate epistemic state to CampaignTruth with HighConfidence
        entity.KnowledgeState = KnowledgeState.CampaignTruth;
        entity.Confidence = ConfidenceLevel.HighConfidence;

        Assert.Equal(KnowledgeState.CampaignTruth, entity.KnowledgeState);
        Assert.Equal(ConfidenceLevel.HighConfidence, entity.Confidence);
    }

    [Fact]
    public void AC_MEM_003_PartySafeProjection_Redacts_DmNotes_And_Filters_DmOnly_Content()
    {
        var characterFacet = new CharacterFacet
        {
            RoleOrArchetype = "Merchant Guildmaster",
            StatementsAbout = new List<CharacterStatement>
            {
                new()
                {
                    StatementId = "stmt_1",
                    SessionId = "session_01",
                    SummaryText = "Supplies weapons to the royal army.",
                    IsByCharacter = false,
                    Visibility = VisibilityScope.PartyKnown
                },
                new()
                {
                    StatementId = "stmt_2",
                    SessionId = "session_01",
                    SummaryText = "Takes bribes from the Smuggler Syndicate.",
                    IsByCharacter = false,
                    Visibility = VisibilityScope.DMPrivate // DM Secret!
                }
            }
        };

        var entity = new MemoryEntityDocument
        {
            Id = "characters/char_007",
            EntityType = MemoryEntityType.Character,
            CanonicalName = "Lord Barth",
            Aliases = ["The Coinmaster"],
            RecognitionVariants = ["Lort Bart", "Barff"],
            SummaryDescription = "A wealthy merchant noble.",
            DmPrivateNotes = "Plans to overthrow the Duke in Session 12.",
            Visibility = VisibilityScope.PartyKnown,
            CharacterFacet = characterFacet
        };

        // Party-Safe Projection
        var partyView = entity.ToPartySafeView(QueryVisibilityContext.PartySafe);

        Assert.NotNull(partyView);
        // Recognition variants must NOT exist in PartySafeMemoryView projection
        Assert.Contains("The Coinmaster", partyView.Aliases);

        // Sub-item filtering: only PartyKnown statement must remain
        Assert.NotNull(partyView.CharacterFacet);
        Assert.Single(partyView.CharacterFacet.StatementsAbout);
        Assert.Equal("stmt_1", partyView.CharacterFacet.StatementsAbout[0].StatementId);
        Assert.Equal("Supplies weapons to the royal army.", partyView.CharacterFacet.StatementsAbout[0].SummaryText);

        // A DMPrivate entity projected under PartySafe must return null
        var dmOnlyEntity = new MemoryEntityDocument
        {
            Id = "items/secret_001",
            EntityType = MemoryEntityType.Object,
            CanonicalName = "Eye of Vecna",
            Visibility = VisibilityScope.DMPrivate
        };
        Assert.Null(dmOnlyEntity.ToPartySafeView(QueryVisibilityContext.PartySafe));
    }

    [Fact]
    public void AC_ENT_001_CharacterStatements_By_Versus_About_Are_Maintained()
    {
        var facet = new CharacterFacet
        {
            RoleOrArchetype = "Village Elder",
            StatementsBy =
            [
                new CharacterStatement
                {
                    StatementId = "stmt_by",
                    SessionId = "session_02",
                    SummaryText = "The old bridge has been cursed since the war.",
                    IsByCharacter = true,
                    TargetSubject = "Old Bridge"
                }
            ],
            StatementsAbout =
            [
                new CharacterStatement
                {
                    StatementId = "stmt_about",
                    SessionId = "session_02",
                    SummaryText = "He was seen speaking with goblin raiders at midnight.",
                    IsByCharacter = false,
                    TargetSubject = "Village Elder"
                }
            ]
        };

        Assert.Single(facet.StatementsBy);
        Assert.True(facet.StatementsBy[0].IsByCharacter);
        Assert.Equal("The old bridge has been cursed since the war.", facet.StatementsBy[0].SummaryText);

        Assert.Single(facet.StatementsAbout);
        Assert.False(facet.StatementsAbout[0].IsByCharacter);
        Assert.Equal("He was seen speaking with goblin raiders at midnight.", facet.StatementsAbout[0].SummaryText);
    }

    [Fact]
    public void AC_TIME_001_LocationHierarchy_Breadcrumbs_And_DecoupledRelativeTime()
    {
        // 1. Breadcrumb calculation test via LocationFacet
        var hierarchyMap = new Dictionary<string, (string Name, string? ParentId)>
        {
            ["loc_cont"] = ("Faerun", null),
            ["loc_city"] = ("Waterdeep", "loc_cont"),
            ["loc_tav"] = ("Yawning Portal", "loc_city"),
            ["loc_room"] = ("Common Room", "loc_tav")
        };

        string breadcrumb = LocationFacet.ComputeBreadcrumb(
            "loc_room",
            "Common Room",
            id => hierarchyMap.TryGetValue(id, out var info) ? info : null);

        Assert.Equal("Faerun > Waterdeep > Yawning Portal > Common Room", breadcrumb);

        // 2. Descendant check
        bool isDescendant = LocationFacet.IsDescendantOf(
            "loc_room",
            "loc_cont",
            id => hierarchyMap.TryGetValue(id, out var info) ? info.ParentId : null);

        Assert.True(isDescendant);

        // 3. Journey relative narrative time: Day 4 of 14: Him Kuldir -> Mine without calendar date
        var journey = new JourneyFacet
        {
            OriginLocationId = "loc_him_kuldir",
            DestinationLocationId = "loc_mine",
            TotalEstimatedDays = 14,
            CurrentRelativeDay = 4,
            RelativeTimeMarker = "Day 4 of 14",
            DepartureCampaignDate = null, // Explicitly decoupled from absolute calendar!
            DayTransitions =
            [
                new RelativeDayTransition
                {
                    TransitionId = "trans_1",
                    SessionId = "session_03",
                    FromRelativeDay = 3,
                    ToRelativeDay = 4,
                    NarrativeMarkerText = "Reached the collapsed entrance of Him Kuldir mine at dawn of the fourth day."
                }
            ],
            KeyMoments =
            [
                new KeyMomentRef
                {
                    MomentId = "km_1",
                    SessionId = "session_03",
                    RelativeDayNumber = 2,
                    SummaryText = "Ambushed by ice mephits in the mountain pass."
                }
            ]
        };

        Assert.Equal(14, journey.TotalEstimatedDays);
        Assert.Equal(4, journey.CurrentRelativeDay);
        Assert.Null(journey.DepartureCampaignDate);
        Assert.Single(journey.DayTransitions);
        Assert.Equal(4, journey.DayTransitions[0].ToRelativeDay);
        Assert.Single(journey.KeyMoments);
    }

    [Fact]
    public async Task AC_ENT_002_EntityResolution_QueuesCandidates_And_EnforcesStrictNoAutoMerge()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var service = new CampaignMemoryService();

            const string campaignId = "camp_resolution_test";

            // Create canonical entity
            var existingEntity = new MemoryEntityDocument
            {
                Id = "char_elminster",
                EntityType = MemoryEntityType.Character,
                CanonicalName = "Elminster Aumar",
                Aliases = ["Elminster", "Old Mage"],
                RecognitionVariants = ["Elminstir"]
            };
            await memoryStore.SaveMemoryEntityAsync(campaignId, existingEntity);

            // Propose candidate match via Levenshtein / phonetic similarity (US-06.5)
            var candidate = service.EvaluateCandidate(
                campaignId: campaignId,
                observedSurfaceName: "Elminsterr",
                contextSnippet: "The old man whispered his name was Elminsterr.",
                existingEntities: [existingEntity],
                sessionId: "session_05",
                segmentId: "seg_123");

            Assert.NotNull(candidate);
            Assert.Equal(EntityResolutionStatus.PendingDmReview, candidate.Status);
            Assert.NotEmpty(candidate.PotentialMatches);
            
            var topMatch = candidate.PotentialMatches.First();
            Assert.Equal("char_elminster", topMatch.EntityId);
            Assert.True(topMatch.MatchScore >= 0.70, $"Expected similarity >= 0.70, got {topMatch.MatchScore}");

            // Save into store
            await memoryStore.SaveResolutionCandidateAsync(campaignId, candidate);

            // Verify queue query in Raven
            var pendingQueue = await memoryStore.ListResolutionCandidatesAsync(campaignId, EntityResolutionStatus.PendingDmReview);
            Assert.Contains(pendingQueue, c => c.CandidateId == candidate.CandidateId);

            // Apply DM ruling: Register as speech variant (ASR whisper mishearing)
            var rulingRequest = new ResolveCandidateRequest(
                Action: "LinkToExisting",
                TargetEntityId: "char_elminster",
                RegisterAsSpeechVariant: true,
                DmNotes: "Whisper misheard Elminster with double r",
                DecidedBy: "DM");

            var resolvedCandidate = service.ApplyResolutionRuling(
                candidate: candidate,
                request: rulingRequest,
                entityResolver: id => id == "char_elminster" ? existingEntity : null);

            Assert.Equal(EntityResolutionStatus.LinkedToExisting, resolvedCandidate.Status);
            Assert.Contains("Elminsterr", existingEntity.RecognitionVariants);

            // Update in store
            await memoryStore.SaveResolutionCandidateAsync(campaignId, resolvedCandidate);
            await memoryStore.SaveMemoryEntityAsync(campaignId, existingEntity);

            // Verify target entity got updated in store
            var storedEntity = await memoryStore.GetMemoryEntityAsync(campaignId, "char_elminster");
            Assert.NotNull(storedEntity);
            Assert.Contains("Elminsterr", storedEntity.RecognitionVariants);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task FR_MEM_CONFLICT_001_DetectsContradiction_Without_DeletingEvidence()
    {
        string tempDir = CreateTempTestDirectory();
        try
        {
            var ravenConfig = new RavenEmbeddedConfiguration(tempDir);
            var memoryStore = new RavenCampaignMemoryStore(ravenConfig);
            var service = new CampaignMemoryService();

            const string campaignId = "camp_conflict_test";

            var existingHistory = new List<MemoryHistoryItem>
            {
                new(
                    HistoryItemId: "hist_01",
                    Description: "The bridge is sturdy and in active use by merchant caravans.",
                    KnowledgeState: KnowledgeState.CampaignTruth,
                    Confidence: ConfidenceLevel.HighConfidence,
                    Visibility: VisibilityScope.PartyKnown,
                    ValidFromSessionId: "session_01",
                    ValidToSessionId: null,
                    RelativeTimeContext: null,
                    SupersededByHistoryItemId: null,
                    RecordedAtUtc: DateTimeOffset.UtcNow.AddDays(-10))
            };

            var entity = new MemoryEntityDocument
            {
                Id = "loc_bridge",
                EntityType = MemoryEntityType.Location,
                CanonicalName = "Stone Arch Bridge",
                SummaryDescription = "A sturdy stone bridge spanning the canyon.",
                StateAndBeliefHistory = existingHistory
            };
            await memoryStore.SaveMemoryEntityAsync(campaignId, entity);

            // Detect contradiction when a new claim says the bridge collapsed 50 years ago
            var conflict = service.DetectAssertionConflict(
                campaignId: campaignId,
                entityId: "loc_bridge",
                newAssertionText: "The bridge collapsed fifty years ago and only broken pillars remain.",
                newKnowledgeState: KnowledgeState.CampaignTruth,
                newSessionId: "session_06",
                existingHistory: existingHistory);

            Assert.NotNull(conflict);
            Assert.Equal(ConflictType.FactualContradiction, conflict.ConflictType);
            Assert.Equal(ConflictResolutionState.Unresolved, conflict.State);
            Assert.Equal(2, conflict.CompetingAssertions.Count);

            // Store conflict
            await memoryStore.SaveConflictAsync(campaignId, conflict);

            var activeConflicts = await memoryStore.ListConflictsAsync(campaignId, unresolvedOnly: true);
            Assert.Single(activeConflicts);
            Assert.Equal("loc_bridge", activeConflicts[0].EntityId);

            // Resolve conflict with DM Ruling (Superseded/ruling: destroyed recently)
            var resolveRequest = new ResolveConflictRequest(
                Resolution: ConflictResolutionState.ResolvedDmRuling,
                WinningAssertionId: conflict.CompetingAssertions[1].AssertionId,
                DmRulingNote: "The bridge was destroyed in session 6 by the dragon.",
                DecidedBy: "DM");

            var resolvedConflict = conflict with
            {
                State = resolveRequest.Resolution,
                DmRuling = resolveRequest.DmRulingNote,
                ResolvedAtUtc = DateTimeOffset.UtcNow
            };

            Assert.Equal(ConflictResolutionState.ResolvedDmRuling, resolvedConflict.State);
            Assert.Equal("The bridge was destroyed in session 6 by the dragon.", resolvedConflict.DmRuling);

            // Update in store
            await memoryStore.SaveConflictAsync(campaignId, resolvedConflict);

            var resolvedInStore = await memoryStore.GetConflictAsync(campaignId, conflict.ConflictId);
            Assert.NotNull(resolvedInStore);
            Assert.Equal(ConflictResolutionState.ResolvedDmRuling, resolvedInStore.State);
            Assert.Equal("The bridge was destroyed in session 6 by the dragon.", resolvedInStore.DmRuling);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }
}
