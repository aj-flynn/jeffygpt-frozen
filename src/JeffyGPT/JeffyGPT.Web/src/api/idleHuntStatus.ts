export const IDLE_HUNT_STATUSES = [
  'hunting',
  'stalking prey',
  'lurking',
  'crawling',
  'setting traps',
  'checking traps',
  'fighting cat',
  'sleeping',
] as const;

export type IdleHuntStatus = (typeof IDLE_HUNT_STATUSES)[number];

export function pickIdleHuntStatus(random: () => number = Math.random): IdleHuntStatus {
  const lastIndex = IDLE_HUNT_STATUSES.length - 1;
  const index = Math.min(lastIndex, Math.max(0, Math.floor(random() * IDLE_HUNT_STATUSES.length)));
  return IDLE_HUNT_STATUSES[index] ?? IDLE_HUNT_STATUSES[0];
}
