export interface RunningTool {
  id: string;
  toolName: string;
}

export type HuntEpisodeStatus = 'hunting' | 'hunted';

export interface HuntEpisode {
  id: string;
  userMessageId: string;
  status: HuntEpisodeStatus;
  calledTools: RunningTool[];
}

export function huntEpisodeId(userMessageId: string): string {
  return `hunt-${userMessageId}`;
}

export function huntEpisodeLabel(status: HuntEpisodeStatus): 'Hunt' | 'Hunted' {
  return status === 'hunting' ? 'Hunt' : 'Hunted';
}

export function shouldShowHuntBody(status: HuntEpisodeStatus, runningToolCount: number): boolean {
  return status === 'hunting' && runningToolCount > 0;
}

export function shouldShowHuntedToolList(
  status: HuntEpisodeStatus,
  calledToolCount: number,
): boolean {
  return status === 'hunted' && calledToolCount > 0;
}

export function huntedAriaLabel(tools: RunningTool[]): string {
  const names = tools.map((tool) => tool.toolName).join(', ');
  return names ? `Hunted. Tools called: ${names}` : 'Hunted';
}

export function markHuntEpisodesComplete(episodes: HuntEpisode[]): HuntEpisode[] {
  return episodes.map((episode) =>
    episode.status === 'hunting' ? { ...episode, status: 'hunted' as const } : episode,
  );
}

export function withHuntingEpisode(
  episodes: HuntEpisode[],
  userMessageId: string | null,
): HuntEpisode[] {
  if (!userMessageId) {
    return episodes;
  }

  const id = huntEpisodeId(userMessageId);
  const existing = episodes.find((episode) => episode.id === id);
  if (existing) {
    if (existing.status === 'hunting') {
      return episodes;
    }

    return episodes.map((episode) =>
      episode.id === id ? { ...episode, status: 'hunting' as const } : episode,
    );
  }

  return [...episodes, { id, userMessageId, status: 'hunting', calledTools: [] }];
}

export function recordHuntTool(
  episodes: HuntEpisode[],
  userMessageId: string | null,
  tool: RunningTool,
): HuntEpisode[] {
  const withEpisode = withHuntingEpisode(episodes, userMessageId);
  if (!userMessageId) {
    return withEpisode;
  }

  const id = huntEpisodeId(userMessageId);
  return withEpisode.map((episode) => {
    if (episode.id !== id) {
      return episode;
    }

    if (episode.calledTools.some((item) => item.id === tool.id)) {
      return episode;
    }

    return { ...episode, calledTools: [...episode.calledTools, tool] };
  });
}

export function huntEpisodeForUserMessage(
  episodes: HuntEpisode[],
  userMessageId: string,
): HuntEpisode | undefined {
  return episodes.find((episode) => episode.userMessageId === userMessageId);
}

export function removeRunningTool(items: RunningTool[], toolCallId: string): RunningTool[] {
  return items.filter((item) => item.id !== toolCallId);
}
