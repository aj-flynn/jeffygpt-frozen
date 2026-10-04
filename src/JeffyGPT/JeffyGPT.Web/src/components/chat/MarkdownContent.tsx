import type { Components } from 'react-markdown';
import ReactMarkdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import remarkGfm from 'remark-gfm';
import './MarkdownContent.css';

export interface MarkdownContentProps {
  content: string;
  className?: string;
  trailing?: React.ReactNode;
}

const markdownComponents: Components = {
  a: ({ node: _node, href, children, ...props }) => (
    <a {...props} href={href} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  ),
  img: () => <span className="markdown-content__img-disabled">[image omitted]</span>,
  pre: ({ children, ...props }) => (
    <pre className="markdown-content__pre" {...props}>
      {children}
    </pre>
  ),
  code: ({ className, children, ...props }) => {
    const isBlock = className?.includes('language-');

    if (isBlock) {
      return (
        <code className={`markdown-content__code-block ${className ?? ''}`} {...props}>
          {children}
        </code>
      );
    }

    return (
      <code className="markdown-content__code-inline" {...props}>
        {children}
      </code>
    );
  },
};

export function MarkdownContent({ content, className, trailing }: MarkdownContentProps) {
  return (
    <div className={['markdown-content', className].filter(Boolean).join(' ')}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={markdownComponents}
      >
        {content}
      </ReactMarkdown>
      {trailing}
    </div>
  );
}
