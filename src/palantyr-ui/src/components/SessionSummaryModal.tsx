import React, { useState } from 'react';
import { SessionSummaryDto, SummaryVisibilityMode } from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

interface SessionSummaryModalProps {
  isOpen: boolean;
  onClose: () => void;
  summary: SessionSummaryDto | null;
  onGenerateSummary?: (mode: SummaryVisibilityMode) => Promise<void>;
  isLoading?: boolean;
  t: TranslationDictionary;
}

export const SessionSummaryModal: React.FC<SessionSummaryModalProps> = ({
  isOpen,
  onClose,
  summary,
  onGenerateSummary,
  isLoading = false,
  t,
}) => {
  const [activeTab, setActiveTab] = useState<SummaryVisibilityMode>(summary?.mode ?? 'PartySafe');
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null);

  if (!isOpen) return null;

  const currentMode = summary?.mode ?? activeTab;

  const handleCopyMarkdown = async (format: 'markdown' | 'discord') => {
    if (!summary) return;

    let text = '';
    if (format === 'discord') {
      text = `**__${summary.title}__**\n` +
        `> *${summary.executiveRecap}*\n\n` +
        `**Key Events:**\n` +
        summary.keyEvents.map(e => `• **${e.description}** ${e.relativeTimeContext ? `(${e.relativeTimeContext})` : ''}`).join('\n') +
        `\n\n**Discoveries & Lore:**\n` +
        summary.majorDiscoveries.map(d => `• **${d.subject}**: ${d.description}`).join('\n');

      if (summary.outstandingQuestions.length > 0) {
        text += `\n\n**Open Threads:**\n` + summary.outstandingQuestions.map(q => `• ${q}`).join('\n');
      }

      if (summary.mode === 'DmFull' && summary.dmPrivateNotes) {
        text += `\n\n||**DM Confidential Notes:**\n${summary.dmPrivateNotes}||`;
      }
    } else {
      text = `# ${summary.title}\n\n` +
        `## Executive Summary\n${summary.executiveRecap}\n\n` +
        `## Key In-World Events\n` +
        summary.keyEvents.map(e => `- **${e.description}** (${e.relativeTimeContext ?? 'In session'})`).join('\n') +
        `\n\n## Major Discoveries\n` +
        summary.majorDiscoveries.map(d => `- **${d.subject}**: ${d.description} [${d.knowledgeState}]`).join('\n') +
        `\n\n## Open Threads\n` +
        summary.outstandingQuestions.map(q => `- ${q}`).join('\n');

      if (summary.mode === 'DmFull' && summary.dmPrivateNotes) {
        text += `\n\n## DM Private Notes\n${summary.dmPrivateNotes}\n`;
      }
    }

    try {
      await navigator.clipboard.writeText(text);
      setCopyFeedback(t.modals.sessionSummary.copySuccess);
      setTimeout(() => setCopyFeedback(null), 2500);
    } catch {
      setCopyFeedback('Failed to copy to clipboard');
      setTimeout(() => setCopyFeedback(null), 2500);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 overflow-y-auto">
      <div className="relative w-full max-w-4xl max-h-[90vh] flex flex-col bg-slate-900 border border-slate-700 rounded-xl shadow-2xl text-slate-100 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-800 px-6 py-4 bg-slate-950/70">
          <div>
            <h2 className="text-xl font-bold tracking-tight text-amber-300">
              {t.modals.sessionSummary.title}
            </h2>
            <p className="text-xs text-slate-400 mt-0.5">
              {t.modals.sessionSummary.subtitle}
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-2 rounded-lg hover:bg-slate-800 transition"
            aria-label={t.modals.sessionSummary.closeBtn}
          >
            ✕
          </button>
        </div>

        {/* Visibility Mode Switcher Tabs */}
        <div className="flex border-b border-slate-800 bg-slate-950/40 px-6 pt-3 gap-2">
          <button
            onClick={() => {
              setActiveTab('PartySafe');
              if (onGenerateSummary && (!summary || summary.mode !== 'PartySafe')) {
                onGenerateSummary('PartySafe');
              }
            }}
            className={`px-4 py-2.5 text-sm font-semibold rounded-t-lg transition flex items-center gap-2 border-t-2 ${
              activeTab === 'PartySafe'
                ? 'bg-slate-900 text-emerald-400 border-emerald-500'
                : 'text-slate-400 hover:text-slate-200 border-transparent'
            }`}
          >
            <span>🛡</span>
            <span>{t.modals.sessionSummary.partySafeTab}</span>
          </button>

          <button
            onClick={() => {
              setActiveTab('DmFull');
              if (onGenerateSummary && (!summary || summary.mode !== 'DmFull')) {
                onGenerateSummary('DmFull');
              }
            }}
            className={`px-4 py-2.5 text-sm font-semibold rounded-t-lg transition flex items-center gap-2 border-t-2 ${
              activeTab === 'DmFull'
                ? 'bg-slate-900 text-amber-400 border-amber-500'
                : 'text-slate-400 hover:text-slate-200 border-transparent'
            }`}
          >
            <span>⚔</span>
            <span>{t.modals.sessionSummary.dmFullTab}</span>
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center py-16 text-slate-400 space-y-3">
              <div className="w-10 h-10 border-4 border-amber-400 border-t-transparent rounded-full animate-spin" />
              <p className="text-sm">Generating context-aware chronicle...</p>
            </div>
          ) : !summary ? (
            <div className="text-center py-16 space-y-4">
              <p className="text-slate-400 text-sm">{t.modals.sessionSummary.emptySummaryNotice}</p>
              {onGenerateSummary && (
                <button
                  onClick={() => onGenerateSummary(activeTab)}
                  className="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-sm font-semibold transition"
                >
                  Generate Summary Now
                </button>
              )}
            </div>
          ) : (
            <>
              {/* Privacy Guarantee Banner */}
              <div
                className={`p-3 rounded-lg border text-xs font-mono flex items-center justify-between ${
                  currentMode === 'PartySafe'
                    ? 'bg-emerald-950/40 border-emerald-800/80 text-emerald-300'
                    : 'bg-amber-950/40 border-amber-800/80 text-amber-300'
                }`}
              >
                <span>
                  {currentMode === 'PartySafe'
                    ? t.modals.sessionSummary.partySafeBadge
                    : t.modals.sessionSummary.dmFullBadge}
                </span>
                <span className="text-[10px] opacity-75">
                  Generated: {new Date(summary.generatedAtUtc).toLocaleTimeString()}
                </span>
              </div>

              {/* Title & Executive Recap */}
              <div className="space-y-2">
                <h3 className="text-lg font-bold text-white">{summary.title}</h3>
                <div className="bg-slate-950/60 border border-slate-800 p-4 rounded-lg text-slate-300 text-sm leading-relaxed whitespace-pre-wrap">
                  {summary.executiveRecap}
                </div>
              </div>

              {/* Key Events */}
              {summary.keyEvents.length > 0 && (
                <div className="space-y-3">
                  <h4 className="text-xs uppercase tracking-wider font-bold text-slate-400">
                    {t.modals.sessionSummary.keyEventsTitle}
                  </h4>
                  <div className="grid gap-2">
                    {summary.keyEvents.map(event => (
                      <div
                        key={event.eventId}
                        className="bg-slate-800/50 border border-slate-700/60 p-3 rounded-lg text-xs flex items-start gap-3"
                      >
                        <span className="text-amber-400 font-bold">•</span>
                        <div className="flex-1 space-y-1">
                          <p className="text-slate-200 font-medium">{event.description}</p>
                          <div className="flex flex-wrap gap-2 text-[10px] text-slate-400">
                            {event.relativeTimeContext && (
                              <span className="bg-slate-700/60 px-2 py-0.5 rounded">
                                ⏱ {event.relativeTimeContext}
                              </span>
                            )}
                            {event.participants.length > 0 && (
                              <span className="bg-slate-700/60 px-2 py-0.5 rounded">
                                👤 {event.participants.join(', ')}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Major Discoveries */}
              {summary.majorDiscoveries.length > 0 && (
                <div className="space-y-3">
                  <h4 className="text-xs uppercase tracking-wider font-bold text-slate-400">
                    {t.modals.sessionSummary.discoveriesTitle}
                  </h4>
                  <div className="grid gap-2">
                    {summary.majorDiscoveries.map((disc, idx) => (
                      <div
                        key={idx}
                        className="bg-slate-800/50 border border-slate-700/60 p-3 rounded-lg text-xs space-y-1"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-semibold text-slate-100">{disc.subject}</span>
                          <span className="text-[10px] px-2 py-0.5 rounded bg-blue-950/60 text-blue-300 border border-blue-800/60">
                            {disc.knowledgeState}
                          </span>
                        </div>
                        <p className="text-slate-300">{disc.description}</p>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Open Threads */}
              {summary.outstandingQuestions.length > 0 && (
                <div className="space-y-3">
                  <h4 className="text-xs uppercase tracking-wider font-bold text-slate-400">
                    {t.modals.sessionSummary.outstandingQuestionsTitle}
                  </h4>
                  <ul className="list-disc list-inside space-y-1 text-xs text-slate-300">
                    {summary.outstandingQuestions.map((q, idx) => (
                      <li key={idx}>{q}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* DM Private Notes (DmFull mode only) */}
              {activeTab === 'DmFull' && summary.dmPrivateNotes && (
                <div className="space-y-2 border-t border-amber-900/50 pt-4">
                  <h4 className="text-xs uppercase tracking-wider font-bold text-amber-400 flex items-center gap-2">
                    <span>⚔</span>
                    <span>{t.modals.sessionSummary.dmPrivateNotesTitle}</span>
                  </h4>
                  <div className="bg-amber-950/30 border border-amber-800/60 p-4 rounded-lg text-amber-200/90 text-xs leading-relaxed whitespace-pre-wrap font-mono">
                    {summary.dmPrivateNotes}
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Footer Actions */}
        <div className="flex flex-wrap items-center justify-between border-t border-slate-800 px-6 py-4 bg-slate-950/80 gap-3">
          <div className="flex items-center gap-2">
            <button
              onClick={() => handleCopyMarkdown('markdown')}
              disabled={!summary}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-slate-200 text-xs font-medium rounded-lg transition border border-slate-700"
            >
              {t.modals.sessionSummary.copyMarkdownBtn}
            </button>
            <button
              onClick={() => handleCopyMarkdown('discord')}
              disabled={!summary}
              className="px-3 py-1.5 bg-indigo-900/60 hover:bg-indigo-800/80 disabled:opacity-50 text-indigo-200 text-xs font-medium rounded-lg transition border border-indigo-700/60"
            >
              {t.modals.sessionSummary.copyDiscordBtn}
            </button>
            {copyFeedback && (
              <span className="text-xs text-emerald-400 font-medium animate-pulse ml-2">
                {copyFeedback}
              </span>
            )}
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold rounded-lg transition"
            >
              {t.modals.sessionSummary.closeBtn}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
