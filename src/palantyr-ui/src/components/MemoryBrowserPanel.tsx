import React, { useState } from 'react';
import {
  MemoryEntityDto,
  MemoryEntityType,
  EntityResolutionCandidateDto,
  MemoryConflictDto,
  ResolveCandidateRequest,
  ResolveConflictRequest,
  CreateMemoryEntityRequest,
  UpdateMemoryEntityRequest,
  PalantyrLanguage,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

interface MemoryBrowserPanelProps {
  t: TranslationDictionary;
  language: PalantyrLanguage;
  campaignId: string;
  entities: MemoryEntityDto[];
  resolutionCandidates: EntityResolutionCandidateDto[];
  conflicts: MemoryConflictDto[];
  onSaveEntity: (entity: CreateMemoryEntityRequest | UpdateMemoryEntityRequest, entityId?: string) => void;
  onDeleteEntity: (entityId: string) => void;
  onResolveCandidate: (candidateId: string, request: ResolveCandidateRequest) => void;
  onResolveConflict: (conflictId: string, request: ResolveConflictRequest) => void;
}

export const MemoryBrowserPanel: React.FC<MemoryBrowserPanelProps> = ({
  t,
  language,
  campaignId,
  entities,
  resolutionCandidates,
  conflicts,
  onSaveEntity,
  onDeleteEntity,
  onResolveCandidate,
  onResolveConflict,
}) => {
  const [selectedType, setSelectedType] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(entities[0]?.id || null);

  // Drawers / Modals
  const [activeDrawer, setActiveDrawer] = useState<'none' | 'resolveAsap' | 'conflicts'>('none');
  const [resolvingCandidate, setResolvingCandidate] = useState<EntityResolutionCandidateDto | null>(null);
  const [resolvingConflict, setResolvingConflict] = useState<MemoryConflictDto | null>(null);
  const [editingEntity, setEditingEntity] = useState<MemoryEntityDto | null>(null);
  const [isNewEntityModalOpen, setIsNewEntityModalOpen] = useState<boolean>(false);

  // Form states for creating/editing entity
  const [formName, setFormName] = useState<string>('');
  const [formType, setFormType] = useState<MemoryEntityType>('Character');
  const [formAliases, setFormAliases] = useState<string>('');
  const [formDescription, setFormDescription] = useState<string>('');
  const [formDmNotes, setFormDmNotes] = useState<string>('');
  const [formVisibility, setFormVisibility] = useState<'PartyKnown' | 'DMPrivate'>('PartyKnown');

  const pendingCandidates = resolutionCandidates.filter((c) => c.status === 'PendingDmReview');
  const unresolvedConflicts = conflicts.filter((c) => c.state === 'Unresolved');

  const filteredEntities = entities.filter((entity) => {
    const matchesType = selectedType === 'ALL' || entity.entityType === selectedType;
    const cleanSearch = searchQuery.trim().toLowerCase();
    const matchesSearch =
      cleanSearch === '' ||
      entity.canonicalName.toLowerCase().includes(cleanSearch) ||
      entity.aliases.some((a) => a.toLowerCase().includes(cleanSearch)) ||
      entity.summaryDescription.toLowerCase().includes(cleanSearch);

    return matchesType && matchesSearch;
  });

  const selectedEntity = entities.find((e) => e.id === selectedEntityId) || filteredEntities[0] || null;

  const handleOpenCreateModal = () => {
    setFormName('');
    setFormType('Character');
    setFormAliases('');
    setFormDescription('');
    setFormDmNotes('');
    setFormVisibility('PartyKnown');
    setEditingEntity(null);
    setIsNewEntityModalOpen(true);
  };

  const handleOpenEditModal = (entity: MemoryEntityDto) => {
    setFormName(entity.canonicalName);
    setFormType(entity.entityType);
    setFormAliases(entity.aliases.join(', '));
    setFormDescription(entity.summaryDescription);
    setFormDmNotes(entity.dmPrivateNotes || '');
    setFormVisibility(entity.visibility === 'DMPrivate' ? 'DMPrivate' : 'PartyKnown');
    setEditingEntity(entity);
    setIsNewEntityModalOpen(true);
  };

  const handleSaveForm = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formName.trim()) return;

    const aliasesArray = formAliases
      .split(',')
      .map((a) => a.trim())
      .filter((a) => a.length > 0);

    if (editingEntity) {
      const updateReq: UpdateMemoryEntityRequest = {
        canonicalName: formName.trim(),
        aliases: aliasesArray,
        summaryDescription: formDescription,
        dmPrivateNotes: formDmNotes.trim() ? formDmNotes.trim() : undefined,
        visibility: formVisibility,
      };
      onSaveEntity(updateReq, editingEntity.id);
    } else {
      const createReq: CreateMemoryEntityRequest = {
        canonicalName: formName.trim(),
        entityType: formType,
        aliases: aliasesArray,
        summaryDescription: formDescription,
        dmPrivateNotes: formDmNotes.trim() ? formDmNotes.trim() : undefined,
        visibility: formVisibility,
        knowledgeState: 'CampaignTruth',
        confidence: 'HighConfidence',
      };
      onSaveEntity(createReq);
    }

    setIsNewEntityModalOpen(false);
  };

  return (
    <div className="memory-browser-container">
      {/* Top Header & Fast Action Drawers */}
      <div className="memory-header-row">
        <div>
          <h2 className="memory-title">📚 {t.memoryBrowser.title}</h2>
          <p className="memory-subtitle">{t.memoryBrowser.subtitle}</p>
        </div>

        <div className="memory-actions-bar">
          <button
            className={`badge-pill ${pendingCandidates.length > 0 ? 'badge-pill-alert' : ''}`}
            onClick={() => setActiveDrawer(activeDrawer === 'resolveAsap' ? 'none' : 'resolveAsap')}
          >
            🔍 {t.memoryBrowser.resolveAsapQueue} ({pendingCandidates.length})
          </button>

          <button
            className={`badge-pill ${unresolvedConflicts.length > 0 ? 'badge-pill-warning' : ''}`}
            onClick={() => setActiveDrawer(activeDrawer === 'conflicts' ? 'none' : 'conflicts')}
          >
            ⚡ {t.memoryBrowser.conflictsQueue} ({unresolvedConflicts.length})
          </button>

          <button className="btn-primary" onClick={handleOpenCreateModal}>
            + {t.memoryBrowser.createNewEntityBtn}
          </button>
        </div>
      </div>

      {/* Resolve ASAP Drawer */}
      {activeDrawer === 'resolveAsap' && (
        <div className="drawer-panel resolve-asap-drawer">
          <div className="drawer-header">
            <h3>⚡ {t.memoryBrowser.resolveAsapQueue}</h3>
            <button className="btn-close" onClick={() => setActiveDrawer('none')}>
              ✕
            </button>
          </div>
          <p className="drawer-help">
            {language === 'Spanish'
              ? 'Nombres detectados en transcripciones con similitud fonética o Levenshtein. Requiere decisión del DM para evitar mezclas automáticas irreversibles.'
              : 'Names detected in transcripts with phonetic or Levenshtein similarity. Explicit DM ruling required—auto-merge is strictly forbidden.'}
          </p>

          {pendingCandidates.length === 0 ? (
            <p className="empty-subtext">
              {language === 'Spanish'
                ? 'No hay candidatos pendientes de resolución.'
                : 'All detected entity candidates are resolved.'}
            </p>
          ) : (
            <div className="candidate-list">
              {pendingCandidates.map((candidate) => (
                <div key={candidate.candidateId} className="candidate-card">
                  <div className="candidate-surface-row">
                    <span className="candidate-name">"{candidate.observedSurfaceName}"</span>
                    <span className="candidate-meta">
                      {candidate.sourceSessionId} &bull; {candidate.sourceSegmentId || 'seg'}
                    </span>
                  </div>

                  {candidate.contextSnippet && (
                    <blockquote className="candidate-snippet">"{candidate.contextSnippet}"</blockquote>
                  )}

                  {candidate.potentialMatches.length > 0 && (
                    <div className="potential-matches">
                      <span className="matches-label">
                        {language === 'Spanish' ? 'Coincidencias Sugeridas:' : 'Suggested Matches:'}
                      </span>
                      {candidate.potentialMatches.map((m) => (
                        <div key={m.entityId} className="match-pill">
                          <strong>{m.canonicalName}</strong> ({Math.round(m.matchScore * 100)}% match &bull;{' '}
                          {m.matchReason})
                          <button
                            className="btn-tiny"
                            onClick={() =>
                              onResolveCandidate(candidate.candidateId, {
                                action: 'LinkToExisting',
                                targetEntityId: m.entityId,
                                registerAsSpeechVariant: false,
                                dmNotes: 'Linked via Memory Browser',
                              })
                            }
                          >
                            🔗 {t.memoryBrowser.linkToExistingBtn}
                          </button>
                          <button
                            className="btn-tiny speech-variant"
                            onClick={() =>
                              onResolveCandidate(candidate.candidateId, {
                                action: 'LinkToExisting',
                                targetEntityId: m.entityId,
                                registerAsSpeechVariant: true,
                                dmNotes: 'Registered acoustic variant',
                              })
                            }
                          >
                            🎙️ {t.memoryBrowser.registerSpeechVariantBtn}
                          </button>
                        </div>
                      ))}
                    </div>
                  )}

                  <div className="candidate-buttons-row">
                    <button
                      className="btn-small-secondary"
                      onClick={() =>
                        onResolveCandidate(candidate.candidateId, {
                          action: 'CreateNew',
                          newEntityCanonicalName: candidate.observedSurfaceName,
                        })
                      }
                    >
                      {t.memoryBrowser.createEntityBtn}
                    </button>
                    <button
                      className="btn-small-secondary text-danger"
                      onClick={() =>
                        onResolveCandidate(candidate.candidateId, {
                          action: 'Dismiss',
                          dmNotes: 'Dismissed by DM',
                        })
                      }
                    >
                      {t.memoryBrowser.dismissBtn}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Lore Conflicts Drawer */}
      {activeDrawer === 'conflicts' && (
        <div className="drawer-panel conflicts-drawer">
          <div className="drawer-header">
            <h3>⚖️ {t.memoryBrowser.conflictsQueue}</h3>
            <button className="btn-close" onClick={() => setActiveDrawer('none')}>
              ✕
            </button>
          </div>
          <p className="drawer-help">
            {language === 'Spanish'
              ? 'Aserciones contradictorias sobre una misma entidad. El sistema preserva ambas versiones con su fuente hasta que el DM decida la resolución.'
              : 'Contradictory assertions detected across sessions. Historical evidence is strictly preserved until DM provides an authoritative ruling.'}
          </p>

          {unresolvedConflicts.length === 0 ? (
            <p className="empty-subtext">
              {language === 'Spanish' ? 'No hay contradicciones de lore activas.' : 'No active lore contradictions.'}
            </p>
          ) : (
            <div className="conflict-list">
              {unresolvedConflicts.map((conflict) => (
                <div key={conflict.conflictId} className="conflict-card">
                  <h4 className="conflict-summary">{conflict.summary}</h4>
                  <div className="competing-assertions-grid">
                    {conflict.competingAssertions.map((assertion, idx) => (
                      <div key={assertion.assertionId} className="assertion-box">
                        <span className="assertion-badge">
                          {language === 'Spanish' ? `Aserción ${idx + 1}` : `Claim ${idx + 1}`} ({assertion.knowledgeState})
                        </span>
                        <p className="assertion-text">"{assertion.assertionText}"</p>
                        <button
                          className="btn-tiny"
                          onClick={() =>
                            onResolveConflict(conflict.conflictId, {
                              resolution: 'ResolvedDmRuling',
                              winningAssertionId: assertion.assertionId,
                              dmRulingNote: `Accepted claim ${idx + 1}`,
                            })
                          }
                        >
                          👑 {language === 'Spanish' ? 'Establecer como Canon' : 'Declare Canon'}
                        </button>
                      </div>
                    ))}
                  </div>

                  <div className="conflict-actions-footer">
                    <button
                      className="btn-small-secondary"
                      onClick={() =>
                        onResolveConflict(conflict.conflictId, {
                          resolution: 'ResolvedSuperseded',
                          dmRulingNote: 'Temporal Evolution: State changed over time during campaign',
                        })
                      }
                    >
                      ⏳ {t.memoryBrowser.temporalEvolutionBtn}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="memory-controls-bar">
        <div className="type-chips">
          {[
            { id: 'ALL', label: t.memoryBrowser.allTypes },
            { id: 'Character', label: t.memoryBrowser.characterTab },
            { id: 'Location', label: t.memoryBrowser.locationTab },
            { id: 'Organization', label: t.memoryBrowser.factionTab },
            { id: 'Object', label: t.memoryBrowser.objectTab },
            { id: 'Quest', label: t.memoryBrowser.questTab },
            { id: 'JourneyOrStay', label: t.memoryBrowser.journeyTab },
          ].map((tab) => (
            <button
              key={tab.id}
              className={`type-chip ${selectedType === tab.id ? 'selected' : ''}`}
              onClick={() => setSelectedType(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <input
          type="text"
          className="search-input"
          placeholder={t.memoryBrowser.searchPlaceholder}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
      </div>

      {/* Main Split View: Entities List & Entity Detail Card */}
      <div className="memory-split-layout">
        <div className="entity-list-pane">
          {filteredEntities.length === 0 ? (
            <div className="empty-panel-state">
              <p>{t.memoryBrowser.emptyState}</p>
            </div>
          ) : (
            filteredEntities.map((entity) => {
              const isSelected = entity.id === selectedEntity?.id;
              return (
                <div
                  key={entity.id}
                  className={`entity-item-row ${isSelected ? 'selected' : ''}`}
                  onClick={() => setSelectedEntityId(entity.id)}
                >
                  <div className="entity-item-main">
                    <span className="entity-item-name">{entity.canonicalName}</span>
                    <span className="entity-item-type">{entity.entityType}</span>
                  </div>

                  <div className="entity-item-badges">
                    {entity.visibility === 'DMPrivate' ? (
                      <span className="badge-chip badge-dm">{t.memoryBrowser.dmPrivateBadge}</span>
                    ) : (
                      <span className="badge-chip badge-party">{t.memoryBrowser.partySafeBadge}</span>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </div>

        <div className="entity-detail-pane">
          {selectedEntity ? (
            <div className="entity-card">
              <div className="entity-card-header">
                <div>
                  <div className="entity-type-tag">{selectedEntity.entityType}</div>
                  <h3 className="entity-canonical-name">{selectedEntity.canonicalName}</h3>
                  {selectedEntity.titles.length > 0 && (
                    <div className="entity-titles">{selectedEntity.titles.join(' &bull; ')}</div>
                  )}
                </div>

                <div className="entity-header-actions">
                  <button className="btn-small-secondary" onClick={() => handleOpenEditModal(selectedEntity)}>
                    ✏️ {language === 'Spanish' ? 'Editar' : 'Edit'}
                  </button>
                  <button
                    className="btn-small-secondary text-danger"
                    onClick={() => onDeleteEntity(selectedEntity.id)}
                  >
                    🗑️ {language === 'Spanish' ? 'Borrar' : 'Delete'}
                  </button>
                </div>
              </div>

              {/* Epistemic & Confidence Bar */}
              <div className="epistemic-badges-bar">
                <span className="epistemic-badge">
                  📖 {selectedEntity.knowledgeState} &bull; {selectedEntity.confidence}
                </span>
                <span
                  className={`visibility-badge ${
                    selectedEntity.visibility === 'DMPrivate' ? 'badge-dm' : 'badge-party'
                  }`}
                >
                  🔒 {selectedEntity.visibility === 'DMPrivate' ? t.memoryBrowser.dmPrivateBadge : t.memoryBrowser.partySafeBadge}
                </span>
              </div>

              {/* Summary */}
              <div className="entity-section">
                <p className="entity-summary">{selectedEntity.summaryDescription || '(No description provided)'}</p>
              </div>

              {/* Aliases & Speech Recognition Variants */}
              <div className="entity-section">
                <h4>🏷️ {t.memoryBrowser.aliasesLabel}</h4>
                <div className="tags-row">
                  {selectedEntity.aliases.length > 0 ? (
                    selectedEntity.aliases.map((alias) => (
                      <span key={alias} className="tag-alias">
                        {alias}
                      </span>
                    ))
                  ) : (
                    <span className="empty-tag-note">None</span>
                  )}
                </div>

                <h4 className="mt-2">🎙️ {t.memoryBrowser.speechVariantsLabel}</h4>
                <div className="tags-row">
                  {selectedEntity.recognitionVariants.length > 0 ? (
                    selectedEntity.recognitionVariants.map((variant) => (
                      <span key={variant} className="tag-variant">
                        {variant}
                      </span>
                    ))
                  ) : (
                    <span className="empty-tag-note">None</span>
                  )}
                </div>
              </div>

              {/* Location Breadcrumb & Containment */}
              {selectedEntity.locationFacet && (
                <div className="entity-section location-facet-box">
                  <h4>🗺️ {t.memoryBrowser.breadcrumbLabel}</h4>
                  <div className="breadcrumb-display">
                    {selectedEntity.locationFacet.parentLocationId
                      ? `${selectedEntity.locationFacet.parentLocationId} > ${selectedEntity.canonicalName}`
                      : selectedEntity.canonicalName}
                  </div>
                  {selectedEntity.locationFacet.isTransitPoint && (
                    <div className="transit-info-badge">
                      🚗 {t.memoryBrowser.transitRefLabel}:{' '}
                      {selectedEntity.locationFacet.transitEndpoints?.originLocationId} &rarr;{' '}
                      {selectedEntity.locationFacet.transitEndpoints?.destinationLocationId}
                    </div>
                  )}
                </div>
              )}

              {/* Character Facet Statements */}
              {selectedEntity.characterFacet && (
                <div className="entity-section character-facet-box">
                  <h4>🗣️ {t.memoryBrowser.statementsByLabel}</h4>
                  {selectedEntity.characterFacet.statementsBy.length > 0 ? (
                    <ul className="statement-list">
                      {selectedEntity.characterFacet.statementsBy.map((stmt) => (
                        <li key={stmt.statementId} className="statement-item">
                          "{stmt.summaryText}"
                          {stmt.targetSubject && <span className="subject-tag"> &rarr; {stmt.targetSubject}</span>}
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="empty-subtext">No statements recorded.</p>
                  )}

                  <h4 className="mt-3">👥 {t.memoryBrowser.statementsAboutLabel}</h4>
                  {selectedEntity.characterFacet.statementsAbout.length > 0 ? (
                    <ul className="statement-list">
                      {selectedEntity.characterFacet.statementsAbout.map((stmt) => (
                        <li key={stmt.statementId} className="statement-item">
                          "{stmt.summaryText}"
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="empty-subtext">No statements recorded.</p>
                  )}
                </div>
              )}

              {/* Journey Relative Timeline */}
              {selectedEntity.journeyFacet && (
                <div className="entity-section journey-facet-box">
                  <h4>⏳ {t.memoryBrowser.relativeTimelineLabel}</h4>
                  <div className="relative-timeline-header">
                    <span className="timeline-marker">
                      {selectedEntity.journeyFacet.relativeTimeMarker ||
                        `Day ${selectedEntity.journeyFacet.currentRelativeDay || 1} of ${
                          selectedEntity.journeyFacet.totalEstimatedDays || '?'
                        }`}
                    </span>
                    <span className="timeline-endpoints">
                      {selectedEntity.journeyFacet.originLocationId} &rarr;{' '}
                      {selectedEntity.journeyFacet.destinationLocationId}
                    </span>
                  </div>

                  {selectedEntity.journeyFacet.dayTransitions.length > 0 && (
                    <div className="day-transitions-list">
                      {selectedEntity.journeyFacet.dayTransitions.map((dt) => (
                        <div key={dt.transitionId} className="transition-row">
                          <span className="day-badge">Day {dt.toRelativeDay}</span>
                          <span className="narrative-marker">"{dt.narrativeMarkerText}"</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* DM Private Notes */}
              {selectedEntity.dmPrivateNotes && (
                <div className="entity-section dm-secret-section">
                  <h4>🔒 {t.memoryBrowser.dmPrivateBadge} (Private)</h4>
                  <p className="dm-secret-text">{selectedEntity.dmPrivateNotes}</p>
                </div>
              )}
            </div>
          ) : (
            <div className="empty-panel-state">
              <p>{language === 'Spanish' ? 'Selecciona una entidad para ver sus detalles.' : 'Select an entity to view details.'}</p>
            </div>
          )}
        </div>
      </div>

      {/* Create / Edit Entity Modal */}
      {isNewEntityModalOpen && (
        <div className="modal-overlay">
          <div className="modal-dialog">
            <div className="modal-header">
              <h3>{editingEntity ? t.memoryBrowser.editEntityTitle : t.memoryBrowser.createNewEntityBtn}</h3>
              <button className="btn-close" onClick={() => setIsNewEntityModalOpen(false)}>
                ✕
              </button>
            </div>

            <form onSubmit={handleSaveForm} className="modal-form">
              <div className="form-group">
                <label>{language === 'Spanish' ? 'Nombre Canónico *' : 'Canonical Name *'}</label>
                <input
                  type="text"
                  required
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  placeholder="e.g. Elminster Aumar"
                />
              </div>

              {!editingEntity && (
                <div className="form-group">
                  <label>{language === 'Spanish' ? 'Tipo de Entidad' : 'Entity Type'}</label>
                  <select
                    value={formType}
                    onChange={(e) => setFormType(e.target.value as MemoryEntityType)}
                  >
                    <option value="Character">Character</option>
                    <option value="Location">Location</option>
                    <option value="Organization">Organization / Faction</option>
                    <option value="Object">Object / Item</option>
                    <option value="Quest">Quest</option>
                    <option value="SocialEvent">Social Event</option>
                    <option value="JourneyOrStay">Journey / Expedition</option>
                  </select>
                </div>
              )}

              <div className="form-group">
                <label>{t.memoryBrowser.aliasesLabel} (comma-separated)</label>
                <input
                  type="text"
                  value={formAliases}
                  onChange={(e) => setFormAliases(e.target.value)}
                  placeholder="Old Mage, The Sage of Shadowdale"
                />
              </div>

              <div className="form-group">
                <label>{language === 'Spanish' ? 'Descripción Pública' : 'Public Summary Description'}</label>
                <textarea
                  rows={3}
                  value={formDescription}
                  onChange={(e) => setFormDescription(e.target.value)}
                  placeholder="Summary known to party..."
                />
              </div>

              <div className="form-group">
                <label>{t.memoryBrowser.dmPrivateBadge} (Private to DM only)</label>
                <textarea
                  rows={2}
                  value={formDmNotes}
                  onChange={(e) => setFormDmNotes(e.target.value)}
                  placeholder="Secrets, true motives, or plot twists..."
                />
              </div>

              <div className="form-group">
                <label>{language === 'Spanish' ? 'Visibilidad' : 'Visibility Scope'}</label>
                <select
                  value={formVisibility}
                  onChange={(e) => setFormVisibility(e.target.value as 'PartyKnown' | 'DMPrivate')}
                >
                  <option value="PartyKnown">Party Safe (General World / Party Known)</option>
                  <option value="DMPrivate">DM Secret (Strictly Hidden from Players)</option>
                </select>
              </div>

              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setIsNewEntityModalOpen(false)}>
                  {t.memoryBrowser.cancelBtn}
                </button>
                <button type="submit" className="btn-primary">
                  {t.memoryBrowser.saveEntityBtn}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
