import jeffyGptIcon from '../../assets/brand/jeffygpt-icon.svg';
import './JeffyGptLogo.css';

export interface JeffyGptLogoProps {
  size?: number;
  showWordmark?: boolean;
  showIcon?: boolean;
  className?: string;
}

export function JeffyGptLogo({
  size = 40,
  showWordmark = false,
  showIcon = true,
  className = '',
}: JeffyGptLogoProps) {
  const classes = ['jgpt-logo', className].filter(Boolean).join(' ');

  return (
    <div className={classes}>
      {showIcon && (
        <img
          className="jgpt-logo__icon"
          src={jeffyGptIcon}
          alt={showWordmark ? '' : 'JeffyGPT'}
          width={size}
          height={size}
        />
      )}
      {showWordmark && (
        <span className="jgpt-logo__wordmark" aria-label="JeffyGPT">
          <span className="jgpt-logo__jeffy">Jeffy</span>{' '}
          <span className="jgpt-logo__gpt">GPT</span>
        </span>
      )}
    </div>
  );
}
