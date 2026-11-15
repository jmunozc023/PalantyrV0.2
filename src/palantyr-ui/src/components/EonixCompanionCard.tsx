import React, { useState } from 'react';
import {
  EonixChatMessage,
  EonixState,
  VisibilityScope,
} from '../types/palantyr';
import { TranslationDictionary } from '../i18n/translations';

export interface EonixCompanionCardProps {
  eonixState: EonixState;
  reduceAnimations: boolean;
  needsAttentionCount: number;
  messages: EonixChatMessage[];
  onSendMessage: (
    text: string,
    mode: 'ask' | 'teach',
    visibility: VisibilityScope,
    scope?: 'CampaignDmFull' | 'PartyKnowledge' | 'CharacterScoped'
  ) => void;
  onSelectStatePreview?: (state: EonixState) => void;
  t: TranslationDictionary;
}

const ALL_EONIX_STATES: EonixState[] = [
  'Idle',
  'ListeningRecording',
  'Transcribing',
  'MemoryProcessing',
  'Thinking',
  'Answering',
  'NeedsAttention',
  'Paused',
  'Error',
];

export const EonixCompanionCard: React.FC<EonixCompanionCardProps> = ({
  eonixState,
  reduceAnimations,
  needsAttentionCount,
  messages,
  onSendMessage,
  onSelectStatePreview,
  t,
}) => {
  const [activeMode, setActiveMode] = useState<'ask' | 'teach'>('ask');
  const [inputText, setInputText] = useState('');
  const [teachVisibility, setTeachVisibility] =
    useState<VisibilityScope>('PartyKnown');
  const [activeScope, setActiveScope] = useState<'CampaignDmFull' | 'PartyKnowledge'>('PartyKnowledge');
  const [selectedCitation, setSelectedCitation] = useState<string | null>(null);

  const stateMeta = t.eonixStates[eonixState];

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = inputText.trim();
    if (!trimmed) return;
    onSendMessage(trimmed, activeMode, teachVisibility, activeScope);
    setInputText('');
  };

  const handleQuickPrompt = (promptText: string) => {
    setActiveMode('ask');
    onSendMessage(promptText, 'ask', 'PartyKnown', activeScope);
  };

  return (
    <section
      className={`eonix-companion-card state-${eonixState.toLowerCase()} ${
        reduceAnimations ? 'reduced-motion' : 'animated-motion'
      }`}
      aria-label={t.eonixCompanion.title}
    >
      <div className="eonix-header-row">
        <div className="eonix-identity">
          <span className="eonix-crest-title">{t.eonixCompanion.name}</span>
          <span className="eonix-subtitle">{t.eonixCompanion.title}</span>
        </div>
        <div className="eonix-header-badges">
          {/* Knowledge Scope Selector Pill (ADR-007, US-10.2) */}
          <button
            type="button"
            onClick={() => setActiveScope(prev => prev === 'PartyKnowledge' ? 'CampaignDmFull' : 'PartyKnowledge')}
            className={`badge cursor-pointer transition ${
              activeScope === 'PartyKnowledge'
                ? 'bg-emerald-950/80 text-emerald-300 border border-emerald-700/80'
                : 'bg-amber-950/80 text-amber-300 border border-amber-700/80'
            }`}
            title="Click to toggle Eonix Knowledge Scope"
          >
            {activeScope === 'PartyKnowledge' ? '🛡 Party Safe Scope' : '⚔ DM Full Scope'}
          </button>
          <span className="badge badge-private" title="UX-EONIX-001">
            🔒 {t.eonixCompanion.privacyBadge}
          </span>
          {reduceAnimations && (
            <span className="badge badge-reduced-motion" title="AC-USAB-004">
              ⚡ {t.eonixCompanion.animationsReducedBadge}
            </span>
          )}
        </div>
      </div>

      {/* Dragon Visual Stage + State Readout */}
      <div className="eonix-stage-grid">
        <div
          className="eonix-avatar-frame"
          role="img"
          aria-label={stateMeta.ariaDescription}
        >
          <EonixDragonSvg
            state={eonixState}
            reduceAnimations={reduceAnimations}
          />
          <div className="eonix-state-pill" data-state={eonixState}>
            <span className="eonix-state-icon" aria-hidden="true">
              {getEonixStateSymbol(eonixState)}
            </span>
            <span className="eonix-state-text">{stateMeta.badge}</span>
          </div>
        </div>

        <div className="eonix-status-narrative">
          <p className="eonix-status-message">{stateMeta.statusMessage}</p>

          {needsAttentionCount > 0 && (
            <div className="eonix-attention-callout" role="status">
              <strong>⚑ {needsAttentionCount}</strong>{' '}
              {t.welcome.needsAttentionBadge}
            </div>
          )}

          {/* Optional State Inspector Bar so the DM or QA reviewer can inspect all 9 Eonix visual states */}
          {onSelectStatePreview && (
            <div
              className="eonix-state-switcher"
              aria-label="Eonix 9-State Visual Inspector"
            >
              {ALL_EONIX_STATES.map((st) => (
                <button
                  key={st}
                  type="button"
                  className={`state-chip ${eonixState === st ? 'active' : ''}`}
                  onClick={() => onSelectStatePreview(st)}
                  title={t.eonixStates[st].badge}
                >
                  {getEonixStateSymbol(st)} {st}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Private Text-Only Ask / Teach Eonix Bar (UX-CAM-003, UX-EONIX-001) */}
      <div className="eonix-interaction-dock">
        <div className="eonix-mode-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeMode === 'ask'}
            className={`eonix-tab ${activeMode === 'ask' ? 'active' : ''}`}
            onClick={() => setActiveMode('ask')}
          >
            🔮 {t.eonixCompanion.askModeTab}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeMode === 'teach'}
            className={`eonix-tab ${activeMode === 'teach' ? 'active' : ''}`}
            onClick={() => setActiveMode('teach')}
          >
            📜 {t.eonixCompanion.teachModeTab}
          </button>
        </div>

        {activeMode === 'teach' && (
          <div className="eonix-teach-scope-bar">
            <label htmlFor="eonix-teach-visibility">
              {t.eonixCompanion.visibilitySelectorLabel}
            </label>
            <select
              id="eonix-teach-visibility"
              value={teachVisibility}
              onChange={(e) =>
                setTeachVisibility(e.target.value as VisibilityScope)
              }
            >
              {(
                [
                  'GeneralWorld',
                  'PartyKnown',
                  'CharacterScoped',
                  'DMPrivate',
                ] as VisibilityScope[]
              ).map((scope) => (
                <option key={scope} value={scope}>
                  {t.visibilityScopes[scope].label}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="eonix-chat-transcript" aria-live="polite">
          {messages.length === 0 ? (
            <div className="eonix-chat-bubble eonix">
              <div className="bubble-meta">EONIX • PALANTYR ARCHIVIST</div>
              <p>{t.eonixCompanion.sampleWelcomeMessage}</p>
            </div>
          ) : (
            messages.map((msg) => (
              <div
                key={msg.id}
                className={`eonix-chat-bubble ${msg.sender} mode-${msg.mode}`}
              >
                <div className="bubble-meta">
                  <span>
                    {msg.sender === 'dm' ? 'DUNGEON MASTER' : 'EONIX'}
                  </span>
                  {msg.visibility && (
                    <span className={`scope-tag scope-${msg.visibility}`}>
                      {t.visibilityScopes[msg.visibility].label}
                    </span>
                  )}
                  {msg.knowledgeState && (
                    <span className="epistemic-tag">
                      {t.knowledgeStates[msg.knowledgeState]}
                    </span>
                  )}
                </div>
                <p>{msg.text}</p>
                {msg.citations && msg.citations.length > 0 && (
                  <div className="bubble-citations flex flex-wrap gap-1.5 mt-2">
                    {msg.citations.map((cit, i) => (
                      <button
                        key={i}
                        type="button"
                        onClick={() => setSelectedCitation(cit)}
                        className="citation-chip cursor-pointer hover:bg-amber-900/60 transition text-amber-300 border border-amber-700/60 rounded px-2 py-0.5 text-[10px] flex items-center gap-1"
                        title="Click to view grounded evidence source"
                      >
                        <span>📎</span>
                        <span>{cit}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ))
          )}
        </div>

        <div className="eonix-quick-prompts">
          <span className="quick-label">
            {t.eonixCompanion.quickPromptsTitle}
          </span>
          <button
            type="button"
            className="quick-prompt-btn"
            onClick={() => handleQuickPrompt(t.eonixCompanion.quickPrompt1)}
          >
            {t.eonixCompanion.quickPrompt1}
          </button>
          <button
            type="button"
            className="quick-prompt-btn"
            onClick={() => handleQuickPrompt(t.eonixCompanion.quickPrompt2)}
          >
            {t.eonixCompanion.quickPrompt2}
          </button>
          <button
            type="button"
            className="quick-prompt-btn"
            onClick={() => handleQuickPrompt(t.eonixCompanion.quickPrompt3)}
          >
            {t.eonixCompanion.quickPrompt3}
          </button>
        </div>

        <form className="eonix-input-form" onSubmit={handleSubmit}>
          <input
            type="text"
            className="eonix-text-input"
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            placeholder={
              activeMode === 'ask'
                ? t.eonixCompanion.askPlaceholder
                : t.eonixCompanion.teachPlaceholder
            }
            aria-label={
              activeMode === 'ask'
                ? t.eonixCompanion.askPlaceholder
                : t.eonixCompanion.teachPlaceholder
            }
          />
          <button type="submit" className="btn btn-gold eonix-send-btn">
            {t.eonixCompanion.sendButton}
          </button>
        </form>
      </div>

      {/* Grounded Evidence Drawer (ADR-007, US-10.5, AC-EONIX-003) */}
      {selectedCitation && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-xs p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-xl max-w-md w-full p-5 shadow-2xl text-slate-100 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <span className="text-amber-400 font-bold">📎</span>
                <h4 className="text-sm font-bold text-amber-300">
                  {t.evidenceDrawer?.drawerTitle ?? 'Grounded Evidence Source'}
                </h4>
              </div>
              <button
                type="button"
                onClick={() => setSelectedCitation(null)}
                className="text-slate-400 hover:text-white p-1 rounded"
              >
                ✕
              </button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="bg-emerald-950/40 border border-emerald-800/80 p-2.5 rounded-lg text-emerald-300 font-mono text-[11px] flex items-center justify-between">
                <span>{t.evidenceDrawer?.groundedBadge ?? '✓ GROUNDED IN CANON MEMORY'}</span>
                <span className="text-slate-400 font-mono">100% Certainty</span>
              </div>

              <div className="space-y-1">
                <label className="text-[10px] uppercase font-bold text-slate-400">
                  {t.evidenceDrawer?.snippetLabel ?? 'Evidence Citation'}:
                </label>
                <div className="bg-slate-950 border border-slate-800 p-3 rounded-lg text-slate-200 leading-relaxed font-mono text-[11px]">
                  {selectedCitation}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-2 text-[11px] text-slate-400 pt-1">
                <div>
                  <span className="font-semibold text-slate-300">Source: </span>
                  <span>Session Transcript</span>
                </div>
                <div>
                  <span className="font-semibold text-slate-300">Scope: </span>
                  <span className={activeScope === 'PartyKnowledge' ? 'text-emerald-400' : 'text-amber-400'}>
                    {activeScope === 'PartyKnowledge' ? 'Party Safe' : 'DM Full'}
                  </span>
                </div>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-800 flex justify-end">
              <button
                type="button"
                onClick={() => setSelectedCitation(null)}
                className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold rounded-lg transition"
              >
                {t.evidenceDrawer?.closeBtn ?? 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
};

function getEonixStateSymbol(state: EonixState): string {
  switch (state) {
    case 'Idle':
      return '🐉';
    case 'ListeningRecording':
      return '🎙';
    case 'Transcribing':
      return '🪶';
    case 'MemoryProcessing':
      return '💎';
    case 'Thinking':
      return '🔮';
    case 'Answering':
      return '📜';
    case 'NeedsAttention':
      return '⚑';
    case 'Paused':
      return '⏸';
    case 'Error':
      return '⚠';
  }
}

interface EonixDragonSvgProps {
  state: EonixState;
  reduceAnimations: boolean;
}

/**
 * Lightweight, deterministic pixel-art / crest SVG representation of Eonix the Dragon
 * across all 9 operational states (UX-HOME-002..003, NFR-USAB-009).
 */
const EonixDragonSvg: React.FC<EonixDragonSvgProps> = ({
  state,
  reduceAnimations,
}) => {
  const palette = getDragonPalette(state);
  const animClass = reduceAnimations
    ? 'dragon-svg-static'
    : `dragon-svg-anim dragon-anim-${state.toLowerCase()}`;

  return (
    <svg
      viewBox="0 0 120 120"
      className={`eonix-dragon-svg ${animClass}`}
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      {/* Outer Runic Halo */}
      <circle
        cx="60"
        cy="60"
        r="54"
        fill="#10131c"
        stroke={palette.halo}
        strokeWidth="3"
        strokeDasharray={state === 'Paused' ? '6 4' : undefined}
      />

      {/* Secondary State Aura Ring */}
      <circle
        cx="60"
        cy="60"
        r="46"
        fill="none"
        stroke={palette.accent}
        strokeWidth="1.5"
        opacity="0.65"
      />

      {/* Left & Right Dragon Wings (Pixel-stepped geometry) */}
      <path
        d={
          state === 'Paused'
            ? 'M22 68 L34 48 L42 58 L36 82 Z M98 68 L86 48 L78 58 L84 82 Z'
            : 'M14 52 L32 30 L44 50 L32 76 Z M106 52 L88 30 L76 50 L88 76 Z'
        }
        fill={palette.wing}
        stroke={palette.outline}
        strokeWidth="2"
      />

      {/* Dragon Horns & Crest */}
      <polygon
        points="42,32 34,14 50,26"
        fill={palette.horn}
        stroke={palette.outline}
        strokeWidth="1.5"
      />
      <polygon
        points="78,32 86,14 70,26"
        fill={palette.horn}
        stroke={palette.outline}
        strokeWidth="1.5"
      />

      {/* Dragon Head & Torso */}
      <rect
        x="42"
        y="26"
        width="36"
        height="30"
        rx="4"
        fill={palette.scales}
        stroke={palette.outline}
        strokeWidth="2"
      />
      <path
        d="M38 56 L82 56 L86 90 L34 90 Z"
        fill={palette.scales}
        stroke={palette.outline}
        strokeWidth="2"
      />

      {/* Golden Chest Plates */}
      <rect x="48" y="60" width="24" height="6" fill={palette.chest} />
      <rect x="50" y="70" width="20" height="6" fill={palette.chest} />
      <rect x="52" y="80" width="16" height="6" fill={palette.chest} />

      {/* Dragon Eyes (Change expression per state) */}
      {state === 'Paused' ? (
        <>
          <line
            x1="47"
            y1="39"
            x2="55"
            y2="39"
            stroke={palette.eye}
            strokeWidth="3"
          />
          <line
            x1="65"
            y1="39"
            x2="73"
            y2="39"
            stroke={palette.eye}
            strokeWidth="3"
          />
        </>
      ) : (
        <>
          <rect x="47" y="35" width="8" height="7" rx="1" fill={palette.eye} />
          <rect x="65" y="35" width="8" height="7" rx="1" fill={palette.eye} />
        </>
      )}

      {/* Snout & Nostrils */}
      <rect x="49" y="45" width="22" height="8" rx="2" fill={palette.snout} />
      <rect x="53" y="47" width="3" height="3" fill="#0d0f17" />
      <rect x="64" y="47" width="3" height="3" fill="#0d0f17" />

      {/* State-Specific Emblem Accessory in Foreground */}
      {state === 'ListeningRecording' && (
        <g>
          <circle cx="60" cy="96" r="9" fill="#d9383a" stroke="#ffd6d6" strokeWidth="2" />
          <circle cx="60" cy="96" r="4" fill="#ffffff" />
        </g>
      )}
      {state === 'Transcribing' && (
        <g>
          <rect x="44" y="90" width="32" height="12" rx="2" fill="#f3e5ab" stroke="#5c4616" strokeWidth="1.5" />
          <line x1="48" y1="94" x2="72" y2="94" stroke="#5c4616" strokeWidth="2" />
          <line x1="48" y1="98" x2="66" y2="98" stroke="#5c4616" strokeWidth="2" />
        </g>
      )}
      {state === 'MemoryProcessing' && (
        <polygon
          points="60,82 70,94 60,106 50,94"
          fill="#5ce1e6"
          stroke="#ffffff"
          strokeWidth="1.5"
        />
      )}
      {(state === 'Thinking' || state === 'Answering') && (
        <circle
          cx="60"
          cy="94"
          r="10"
          fill={state === 'Answering' ? '#f5c542' : '#9b6bff'}
          stroke="#ffffff"
          strokeWidth="2"
        />
      )}
      {state === 'NeedsAttention' && (
        <g>
          <polygon
            points="60,80 73,104 47,104"
            fill="#f59e0b"
            stroke="#111827"
            strokeWidth="2"
          />
          <text
            x="60"
            y="101"
            textAnchor="middle"
            fill="#111827"
            fontSize="14"
            fontWeight="bold"
          >
            !
          </text>
        </g>
      )}
      {state === 'Error' && (
        <g>
          <circle
            cx="60"
            cy="94"
            r="11"
            fill="#ef4444"
            stroke="#fee2e2"
            strokeWidth="2"
          />
          <text
            x="60"
            y="99"
            textAnchor="middle"
            fill="#ffffff"
            fontSize="14"
            fontWeight="bold"
          >
            ✖
          </text>
        </g>
      )}
    </svg>
  );
};

function getDragonPalette(state: EonixState) {
  switch (state) {
    case 'ListeningRecording':
      return {
        halo: '#ef4444',
        accent: '#f87171',
        scales: '#2b3556',
        wing: '#3b4975',
        horn: '#f5c542',
        chest: '#e0a938',
        snout: '#1e2640',
        eye: '#ff6b6b',
        outline: '#d4af37',
      };
    case 'Transcribing':
      return {
        halo: '#38bdf8',
        accent: '#7dd3fc',
        scales: '#233854',
        wing: '#2d4b70',
        horn: '#f5c542',
        chest: '#d4af37',
        snout: '#19293d',
        eye: '#38bdf8',
        outline: '#93c5fd',
      };
    case 'MemoryProcessing':
      return {
        halo: '#2dd4bf',
        accent: '#5eead4',
        scales: '#1f3f47',
        wing: '#2b5863',
        horn: '#f5c542',
        chest: '#d4af37',
        snout: '#162d33',
        eye: '#5eead4',
        outline: '#5eead4',
      };
    case 'Thinking':
      return {
        halo: '#a855f7',
        accent: '#c084fc',
        scales: '#332654',
        wing: '#453370',
        horn: '#f5c542',
        chest: '#d4af37',
        snout: '#241a3d',
        eye: '#d8b4fe',
        outline: '#c084fc',
      };
    case 'Answering':
      return {
        halo: '#f5c542',
        accent: '#fde047',
        scales: '#2e3856',
        wing: '#3f4d75',
        horn: '#fde047',
        chest: '#f5c542',
        snout: '#1f263b',
        eye: '#fde047',
        outline: '#f5c542',
      };
    case 'NeedsAttention':
      return {
        halo: '#f59e0b',
        accent: '#fbbf24',
        scales: '#3d3122',
        wing: '#57442d',
        horn: '#fbbf24',
        chest: '#f59e0b',
        snout: '#292016',
        eye: '#fbbf24',
        outline: '#f59e0b',
      };
    case 'Paused':
      return {
        halo: '#64748b',
        accent: '#94a3b8',
        scales: '#27303f',
        wing: '#333f52',
        horn: '#cbd5e1',
        chest: '#94a3b8',
        snout: '#1e2530',
        eye: '#94a3b8',
        outline: '#64748b',
      };
    case 'Error':
      return {
        halo: '#dc2626',
        accent: '#f87171',
        scales: '#451a1f',
        wing: '#61232b',
        horn: '#fca5a5',
        chest: '#f87171',
        snout: '#2e1014',
        eye: '#ef4444',
        outline: '#ef4444',
      };
    case 'Idle':
    default:
      return {
        halo: '#d4af37',
        accent: '#94a3b8',
        scales: '#263147',
        wing: '#344361',
        horn: '#e6c253',
        chest: '#c9a236',
        snout: '#1b2333',
        eye: '#f5c542',
        outline: '#b89430',
      };
  }
}
