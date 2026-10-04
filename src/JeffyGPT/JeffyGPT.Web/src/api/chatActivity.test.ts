import { describe, expect, it } from 'vitest';
import {
  huntEpisodeForUserMessage,
  huntEpisodeId,
  huntEpisodeLabel,
  huntedAriaLabel,
  markHuntEpisodesComplete,
  recordHuntTool,
  removeRunningTool,
  shouldShowHuntBody,
  shouldShowHuntedToolList,
  withHuntingEpisode,
  type HuntEpisode,
} from './chatActivity';

describe('chatActivity', () => {
  it('builds a deterministic hunt id and Hunt/Hunted labels', () => {
    expect(huntEpisodeId('msg_1')).toBe('hunt-msg_1');
    expect(huntEpisodeLabel('hunting')).toBe('Hunt');
    expect(huntEpisodeLabel('hunted')).toBe('Hunted');
  });

  it('shows the hunt body only while hunting with running tools', () => {
    expect(shouldShowHuntBody('hunting', 1)).toBe(true);
    expect(shouldShowHuntBody('hunting', 0)).toBe(false);
    expect(shouldShowHuntBody('hunted', 1)).toBe(false);
  });

  it('shows the hunted tool list only after tools have been recorded', () => {
    expect(shouldShowHuntedToolList('hunted', 1)).toBe(true);
    expect(shouldShowHuntedToolList('hunted', 0)).toBe(false);
    expect(shouldShowHuntedToolList('hunting', 1)).toBe(false);
    expect(huntedAriaLabel([{ id: 'call_1', toolName: 'get-current-time' }])).toBe(
      'Hunted. Tools called: get-current-time',
    );
  });

  it('starts a hunting episode for a user message and leaves existing hunts in place', () => {
    const started = withHuntingEpisode([], 'msg_1');
    expect(started).toEqual([
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunting', calledTools: [] },
    ]);

    expect(withHuntingEpisode(started, 'msg_1')).toEqual(started);
    expect(withHuntingEpisode(started, null)).toEqual(started);
  });

  it('reopens a completed episode if another tool round starts for the same user message', () => {
    const hunted: HuntEpisode[] = [
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunted', calledTools: [] },
    ];

    expect(withHuntingEpisode(hunted, 'msg_1')).toEqual([
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunting', calledTools: [] },
    ]);
  });

  it('records each started tool on the hunt episode without duplicates', () => {
    const tool = { id: 'call_1', toolName: 'get-current-time' };
    const recorded = recordHuntTool([], 'msg_1', tool);

    expect(recorded).toEqual([
      {
        id: 'hunt-msg_1',
        userMessageId: 'msg_1',
        status: 'hunting',
        calledTools: [tool],
      },
    ]);
    expect(recordHuntTool(recorded, 'msg_1', tool)).toEqual(recorded);
  });

  it('marks hunting episodes complete and removes finished tools', () => {
    const episodes: HuntEpisode[] = [
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunting', calledTools: [] },
      { id: 'hunt-msg_0', userMessageId: 'msg_0', status: 'hunted', calledTools: [] },
    ];

    expect(markHuntEpisodesComplete(episodes)).toEqual([
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunted', calledTools: [] },
      { id: 'hunt-msg_0', userMessageId: 'msg_0', status: 'hunted', calledTools: [] },
    ]);

    expect(
      removeRunningTool(
        [
          { id: 'call_1', toolName: 'get-current-time' },
          { id: 'call_2', toolName: 'read-file' },
        ],
        'call_1',
      ),
    ).toEqual([{ id: 'call_2', toolName: 'read-file' }]);
  });

  it('finds the hunt episode for a user message', () => {
    const episodes: HuntEpisode[] = [
      { id: 'hunt-msg_1', userMessageId: 'msg_1', status: 'hunted', calledTools: [] },
    ];

    expect(huntEpisodeForUserMessage(episodes, 'msg_1')?.status).toBe('hunted');
    expect(huntEpisodeForUserMessage(episodes, 'missing')).toBeUndefined();
  });
});
