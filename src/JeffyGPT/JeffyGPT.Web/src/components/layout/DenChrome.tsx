import { useEffect, useRef, useState } from 'react';
import { JeffyGptLogo } from '../brand/JeffyGptLogo';
import { ThemeToggle } from '../theme/ThemeToggle';
import { pickIdleHuntStatus } from '../../api/idleHuntStatus';
import './DenChrome.css';

/** Static brand chrome. Not a live request counter. */
const DEN_TALLY_LABEL = '2 tonight';

export interface DenChromeProps {
  sending: boolean;
}

export function DenChrome({ sending }: DenChromeProps) {
  const [status, setStatus] = useState(() => pickIdleHuntStatus());
  const wasSendingRef = useRef(sending);

  useEffect(() => {
    if (wasSendingRef.current && !sending) {
      setStatus(pickIdleHuntStatus());
    }
    wasSendingRef.current = sending;
  }, [sending]);

  return (
    <header className="den-chrome">
      <JeffyGptLogo size={32} showWordmark showIcon={false} />
      <span className="den-chrome__tally">{DEN_TALLY_LABEL}</span>
      {/* Decorative idle line; not request progress. */}
      <span className="den-chrome__status">{status}</span>
      <ThemeToggle />
    </header>
  );
}
