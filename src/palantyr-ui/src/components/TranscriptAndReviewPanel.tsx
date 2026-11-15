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
  const [currentAttentionIndex, setCurrentAttentionIndex] = useState<number>(0);
  const [quickCorrectionText, setQuickCorrectionText] = useState<string>('');
  const [isPlayingAudio, setIsPlayingAudio] = useState<boolean>(false);
  const [audioFeedback, setAudioFeedback] = useState<string | null>(null);

  const unresolvedAttentionItems = needsAttentionItems.filter((i) => !i.isResolved);
  const currentAttentionItem = unresolvedAttentionItems[currentAttentionIndex] || unresolvedAttentionItems[0];

  const handlePlayAudioClip = (timestampMs: number) => {
    setIsPlayingAudio(true);
    setAudioFeedback(t.needsAttentionOneScroll?.playingAudio ?? 'Playing 10s audio context...');
    setTimeout(() => {
      setIsPlayingAudio(false);
      setAudioFeedback(null);
    }, 3000);
  };

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
        <div className="attention-content p-4 space-y-4">
          {unresolvedAttentionItems.length === 0 ? (
            <div className="empty-panel-state p-8 text-center bg-slate-900/40 rounded-xl border border-slate-800">
              <p className="text-emerald-400 font-semibold text-sm">
                {t.needsAttentionOneScroll?.allResolvedMessage ?? '🎉 All continuity doubts for this session are resolved!'}
              </p>
            </div>
          ) : (
            <div className="single-scroll-stepper space-y-4">
              {/* Stepper Header */}
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <div className="flex items-center gap-3">
                  <span className="bg-red-950/80 text-red-300 border border-red-800 text-xs px-2.5 py-1 rounded-full font-bold animate-pulse">
                    {t.needsAttentionOneScroll?.resolveAsapBadge ?? '🔥 RESOLVE ASAP'}
                  </span>
                  <span className="text-xs text-slate-400 font-mono">
                    {t.needsAttentionOneScroll?.itemCounter
                      ? t.needsAttentionOneScroll.itemCounter
                          .replace('{current}', String(Math.min(currentAttentionIndex + 1, unresolvedAttentionItems.length)))
                          .replace('{total}', String(unresolvedAttentionItems.length))
                      : `Item ${currentAttentionIndex + 1} of ${unresolvedAttentionItems.length}`}
                  </span>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    disabled={currentAttentionIndex === 0}
                    onClick={() => setCurrentAttentionIndex(prev => Math.max(0, prev - 1))}
                    className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 disabled:opacity-30 text-xs text-slate-300 rounded transition"
                  >
                    ← {language === 'Spanish' ? 'Anterior' : 'Previous'}
                  </button>
                  <button
                    disabled={currentAttentionIndex >= unresolvedAttentionItems.length - 1}
                    onClick={() => setCurrentAttentionIndex(prev => Math.min(unresolvedAttentionItems.length - 1, prev + 1))}
                    className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 disabled:opacity-30 text-xs text-slate-300 rounded transition"
                  >
                    {language === 'Spanish' ? 'Siguiente' : 'Next'} →
                  </button>
                </div>
              </div>

              {/* Current Attention Card ("One Scroll at a Time") */}
              {currentAttentionItem && (
                <div className="bg-slate-900/90 border border-slate-700/80 rounded-xl p-5 shadow-lg space-y-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="space-y-1">
                      <h4 className="text-sm font-bold text-slate-100 flex items-center gap-2">
                        <span>⚠️</span>
                        <span>{currentAttentionItem.title}</span>
                      </h4>
                      <p className="text-xs text-slate-400">
                        Span ID: <span className="font-mono text-slate-300">{currentAttentionItem.targetSpanId}</span>
                      </p>
                    </div>

                    <button
                      onClick={() => handlePlayAudioClip(currentAttentionItem.approximateTimestampMs)}
                      disabled={isPlayingAudio}
                      className="px-3 py-1.5 bg-amber-950/60 hover:bg-amber-900/80 text-amber-300 border border-amber-800/80 text-xs font-semibold rounded-lg transition flex items-center gap-1.5"
                    >
                      <span>{isPlayingAudio ? '🔊' : '▶'}</span>
                      <span>
                        {isPlayingAudio
                          ? (t.needsAttentionOneScroll?.playingAudio ?? 'Playing...')
                          : (t.needsAttentionOneScroll?.audioPreviewBtn ?? 'Play 10s Audio')}
                      </span>
                    </button>
                  </div>

                  {audioFeedback && (
                    <div className="text-xs text-amber-400 font-mono bg-amber-950/30 p-2 rounded border border-amber-900/50">
                      {audioFeedback} (Timestamp: {formatTimestamp(currentAttentionItem.approximateTimestampMs)})
                    </div>
                  )}

                  <div className="bg-slate-950/80 border border-slate-800/80 p-3.5 rounded-lg text-xs leading-relaxed text-slate-200">
                    {language === 'Spanish' ? currentAttentionItem.promptMessageEs : currentAttentionItem.promptMessageEn}
                  </div>

                  {/* Quick Correction Input */}
                  <div className="space-y-1.5">
                    <input
                      type="text"
                      value={quickCorrectionText}
                      onChange={(e) => setQuickCorrectionText(e.target.value)}
                      placeholder={t.needsAttentionOneScroll?.quickCorrectionPlaceholder ?? 'Enter quick DM correction or alias...'}
                      className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 placeholder-slate-500 focus:outline-none focus:border-amber-500"
                    />
                  </div>

                  {/* Multi-Candidate Action Buttons */}
                  <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-800">
                    <button
                      className="px-3 py-1.5 bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-semibold rounded-lg transition"
                      onClick={() => {
                        const note = quickCorrectionText ? `Confirmed: ${quickCorrectionText}` : 'Confirmed as Campaign Canon';
                        onResolveAttentionItem(currentAttentionItem.id, note);
                        setQuickCorrectionText('');
                        if (currentAttentionIndex >= unresolvedAttentionItems.length - 1) {
                          setCurrentAttentionIndex(Math.max(0, unresolvedAttentionItems.length - 2));
                        }
                      }}
                    >
                      {t.needsAttentionOneScroll?.confirmCanonBtn ?? '✓ Confirm Canon'}
                    </button>

                    <button
                      className="px-3 py-1.5 bg-red-800/80 hover:bg-red-700 text-red-100 text-xs font-semibold rounded-lg transition border border-red-700/60"
                      onClick={() => {
                        onResolveAttentionItem(currentAttentionItem.id, 'Rejected lore (persisted resolution tombstone)');
                        setQuickCorrectionText('');
                        if (currentAttentionIndex >= unresolvedAttentionItems.length - 1) {
                          setCurrentAttentionIndex(Math.max(0, unresolvedAttentionItems.length - 2));
                        }
                      }}
                    >
                      {t.needsAttentionOneScroll?.rejectTombstoneBtn ?? '✕ Reject Lore'}
                    </button>

                    <button
                      className="px-3 py-1.5 bg-indigo-900/70 hover:bg-indigo-800 text-indigo-200 text-xs font-semibold rounded-lg transition border border-indigo-700/60"
                      onClick={() => {
                        onResolveAttentionItem(currentAttentionItem.id, `Linked to existing entity: ${quickCorrectionText || 'Canonical Lore'}`);
                        setQuickCorrectionText('');
                        if (currentAttentionIndex >= unresolvedAttentionItems.length - 1) {
                          setCurrentAttentionIndex(Math.max(0, unresolvedAttentionItems.length - 2));
                        }
                      }}
                    >
                      {t.needsAttentionOneScroll?.linkExistingBtn ?? '🔗 Link Existing'}
                    </button>

                    <button
                      className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold rounded-lg transition"
                      onClick={() => {
                        onResolveAttentionItem(currentAttentionItem.id, 'Classified as table-talk joke');
                        setQuickCorrectionText('');
                        if (currentAttentionIndex >= unresolvedAttentionItems.length - 1) {
                          setCurrentAttentionIndex(Math.max(0, unresolvedAttentionItems.length - 2));
                        }
                      }}
                    >
                      {t.needsAttentionOneScroll?.jokeTableTalkBtn ?? '🎭 Joke'}
                    </button>

                    <button
                      className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 text-xs font-semibold rounded-lg transition ml-auto"
                      onClick={() => {
                        setCurrentAttentionIndex(prev => Math.min(unresolvedAttentionItems.length - 1, prev + 1));
                      }}
                    >
                      {t.needsAttentionOneScroll?.deferBtn ?? '⏱ Defer'}
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
