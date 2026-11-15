import React, { useState } from 'react';
import {
  TranscriptSegmentDto,
  WorkingTranscriptSpanDto,
  ConversationContextType,
  NeedsAttentionItemDto,
  PalantyrLanguage,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

interface TranscriptAndReviewPanelProps {
  t: TranslationDictionary;
  language: PalantyrLanguage;
  transcripts: TranscriptSegmentDto[];
  needsAttentionItems: NeedsAttentionItemDto[];
  onCorrectSpan: (segmentId: string, spanId: string, newText: string) => void;
  onPromoteJoke: (segmentId: string, spanId: string, reason: string) => void;
  onResolveAttentionItem: (itemId: string, decision: string) => void;
}

export const TranscriptAndReviewPanel: React.FC<TranscriptAndReviewPanelProps> = ({
  t,
  language,
  transcripts,
  needsAttentionItems,
  onCorrectSpan,
  onPromoteJoke,
  onResolveAttentionItem,
}) => {
  const [selectedSegmentId, setSelectedSegmentId] = useState<string>(
    transcripts[0]?.segmentId || ''
  );
  const [editingSpanId, setEditingSpanId] = useState<string | null>(null);
  const [editText, setEditText] = useState<string>('');
  const [activeTab, setActiveTab] = useState<'transcripts' | 'attention'>('transcripts');

  const activeSegment = transcripts.find((s) => s.segmentId === selectedSegmentId) || transcripts[0];

  const formatTimestamp = (ms: number): string => {
    const totalSeconds = Math.floor(ms / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  };

  const getContextLabel = (type: ConversationContextType): string => {
    switch (type) {
      case 'InWorldCanonCandidate':
        return language === 'Spanish' ? 'Canon de Campaña' : 'In-World Canon';
      case 'OutOfCharacterTableTalk':
        return language === 'Spanish' ? 'Charla Fuera de Rol' : 'Table Talk';
      case 'HypotheticalOrJoke':
        return language === 'Spanish' ? 'Broma / Hipotético' : 'Joke / What-if';
      case 'RulesOrMeta':
        return language === 'Spanish' ? 'Reglas / Meta' : 'Rules / Mechanics';
      case 'Ambiguous':
        return language === 'Spanish' ? 'Contexto Ambiguo' : 'Ambiguous';
    }
  };

  const getContextBadgeClass = (type: ConversationContextType): string => {
    switch (type) {
      case 'InWorldCanonCandidate':
        return 'context-pill canon';
      case 'OutOfCharacterTableTalk':
        return 'context-pill table-talk';
      case 'HypotheticalOrJoke':
        return 'context-pill joke';
      case 'RulesOrMeta':
        return 'context-pill rules';
      case 'Ambiguous':
        return 'context-pill ambiguous';
    }
  };

  const handleStartEdit = (span: WorkingTranscriptSpanDto) => {
    setEditingSpanId(span.spanId);
    setEditText(span.correctedText);
  };

  const handleSaveEdit = (segmentId: string, spanId: string) => {
    if (editText.trim().length > 0) {
      onCorrectSpan(segmentId, spanId, editText.trim());
    }
    setEditingSpanId(null);
  };

  return (
    <div className="transcript-panel card">
      <div className="panel-tabs">
        <button
          className={`tab-btn ${activeTab === 'transcripts' ? 'active' : ''}`}
          onClick={() => setActiveTab('transcripts')}
        >
          📜 {language === 'Spanish' ? 'Transcripción Dual' : 'Dual Transcripts'}
          {transcripts.length > 0 && <span className="tab-count">{transcripts.length}</span>}
        </button>
        <button
          className={`tab-btn ${activeTab === 'attention' ? 'active' : ''}`}
          onClick={() => setActiveTab('attention')}
        >
          ⚠️ {language === 'Spanish' ? 'Needs Attention' : 'Needs Attention'}
          {needsAttentionItems.filter((i) => !i.isResolved).length > 0 && (
            <span className="tab-count alert">
              {needsAttentionItems.filter((i) => !i.isResolved).length}
            </span>
          )}
        </button>
      </div>

      {activeTab === 'transcripts' && (
        <div className="transcript-content">
          {transcripts.length === 0 ? (
            <div className="empty-panel-state">
              <p>
                {language === 'Spanish'
                  ? 'No hay segmentos grabados aún. Inicia la grabación para capturar audio.'
                  : 'No audio segments transcribed yet. Start recording to capture tabletop audio.'}
              </p>
            </div>
          ) : (
            <>
              {/* Segment Selector */}
              <div className="segment-bar">
                <span className="segment-label">
                  {language === 'Spanish' ? 'Segmento:' : 'Segment:'}
                </span>
                <div className="segment-chips">
                  {transcripts.map((seg) => (
                    <button
                      key={seg.segmentId}
                      className={`segment-chip ${seg.segmentId === activeSegment?.segmentId ? 'selected' : ''}`}
                      onClick={() => setSelectedSegmentId(seg.segmentId)}
                    >
                      {seg.segmentId} ({seg.workingSpans.length} spans)
                    </button>
                  ))}
                </div>
              </div>

              {/* Working Spans List */}
              <div className="spans-list">
                {activeSegment?.workingSpans.map((span) => (
                  <div key={span.spanId} className="span-card">
                    <div className="span-header">
                      <span className="span-timestamps">
                        ⏱️ [{formatTimestamp(span.startMs)} – {formatTimestamp(span.endMs)}]
                      </span>
                      <span className={getContextBadgeClass(span.contextType)}>
                        {getContextLabel(span.contextType)}
                      </span>
                      {span.isUserEdited && (
                        <span className="audit-badge">
                          ✏️ {language === 'Spanish' ? 'Corregido por DM' : 'Corrected by DM'}
                        </span>
                      )}
                      {span.promotionReason && (
                        <span className="promotion-badge">
                          ⭐ {language === 'Spanish' ? 'Promovido a Canon' : 'Promoted to Canon'}
                        </span>
                      )}
                    </div>

                    <div className="span-body">
                      {editingSpanId === span.spanId ? (
                        <div className="edit-box">
                          <textarea
                            className="edit-input"
                            value={editText}
                            onChange={(e) => setEditText(e.target.value)}
                            rows={2}
                          />
                          <div className="edit-actions">
                            <button
                              className="btn-primary small"
                              onClick={() => handleSaveEdit(activeSegment.segmentId, span.spanId)}
                            >
                              {language === 'Spanish' ? 'Guardar Corrección' : 'Save Correction'}
                            </button>
                            <button
                              className="btn-secondary small"
                              onClick={() => setEditingSpanId(null)}
                            >
                              {language === 'Spanish' ? 'Cancelar' : 'Cancel'}
                            </button>
                          </div>
                        </div>
                      ) : (
                        <p className="span-text">{span.correctedText}</p>
                      )}

                      {/* Machine Layer transparency */}
                      {span.isUserEdited && (
                        <p className="machine-original">
                          <em>
                            {language === 'Spanish' ? 'Texto original máquina:' : 'Machine text:'} &ldquo;
                            {span.originalText}&rdquo;
                          </em>
                        </p>
                      )}
                    </div>

                    <div className="span-footer">
                      {editingSpanId !== span.spanId && (
                        <button
                          className="btn-text small"
                          onClick={() => handleStartEdit(span)}
                        >
                          ✏️ {language === 'Spanish' ? 'Corregir Texto' : 'Edit Text'}
                        </button>
                      )}

                      {span.contextType === 'HypotheticalOrJoke' && (
                        <button
                          className="btn-text highlight small"
                          onClick={() =>
                            onPromoteJoke(
                              activeSegment.segmentId,
                              span.spanId,
                              language === 'Spanish'
                                ? 'El DM confirmó que la acción realmente sucedió en el mundo'
                                : 'DM confirmed action actually occurred in-world'
                            )
                          }
                        >
                          ⭐ {language === 'Spanish' ? 'Hacer Canon (Joke-to-Canon)' : 'Make Canon'}
                        </button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      )}

      {activeTab === 'attention' && (
        <div className="attention-content">
          {needsAttentionItems.filter((i) => !i.isResolved).length === 0 ? (
            <div className="empty-panel-state">
              <p>
                ✅ {language === 'Spanish'
                  ? 'No hay dudas de continuidad pendientes. ¡Todo está en orden!'
                  : 'No continuity uncertainties pending review. Everything is clear!'}
              </p>
            </div>
          ) : (
            <div className="attention-list">
              {needsAttentionItems
                .filter((i) => !i.isResolved)
                .map((item) => (
                  <div key={item.id} className="attention-card">
                    <div className="attention-header">
                      <span className="attention-title">⚠️ {item.title}</span>
                      <span className="timestamp-badge">
                        ⏱️ {formatTimestamp(item.approximateTimestampMs)}
                      </span>
                    </div>

                    <p className="attention-prompt">
                      {language === 'Spanish' ? item.promptMessageEs : item.promptMessageEn}
                    </p>

                    <div className="attention-actions">
                      <button
                        className="btn-primary small"
                        onClick={() =>
                          onResolveAttentionItem(
                            item.id,
                            language === 'Spanish'
                              ? 'Confirmado como Canon de Campaña'
                              : 'Confirmed as Campaign Canon'
                          )
                        }
                      >
                        ✅ {language === 'Spanish' ? 'Es Canon' : 'It Is Canon'}
                      </button>
                      <button
                        className="btn-secondary small"
                        onClick={() =>
                          onResolveAttentionItem(
                            item.id,
                            language === 'Spanish'
                              ? 'Descartado como broma o charla fuera de rol'
                              : 'Discarded as table-talk joke'
                          )
                        }
                      >
                        ❌ {language === 'Spanish' ? 'Fue Broma / Charla' : 'Table Talk / Joke'}
                      </button>
                    </div>
                  </div>
                ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
