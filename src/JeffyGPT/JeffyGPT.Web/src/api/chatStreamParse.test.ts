import { describe, expect, it } from 'vitest';
import {
  isTerminalStreamEventType,
  MAX_STREAM_LINE_LENGTH,
  parseChatStreamLine,
} from './chatStreamParse';

describe('parseChatStreamLine', () => {
  it('parses assistant_delta events', () => {
    expect(parseChatStreamLine('{"type":"assistant_delta","content":"hi"}')).toEqual({
      type: 'assistant_delta',
      content: 'hi',
    });
  });

  it('rejects invalid JSON', () => {
    expect(() => parseChatStreamLine('{bad')).toThrow(/invalid JSON/i);
  });

  it('rejects missing toolCall payloads', () => {
    expect(() => parseChatStreamLine('{"type":"tool_call_started"}')).toThrow(/toolCall/i);
  });

  it('rejects unknown event types', () => {
    expect(() => parseChatStreamLine('{"type":"nope"}')).toThrow(/Unsupported/i);
  });

  describe('accepted events', () => {
    it('parses a tool call starting', () => {
      expect(
        parseChatStreamLine(
          '{"type":"tool_call_started","toolCall":{"id":"call_1","name":"read-file","argumentsJson":"{}"}}',
        ),
      ).toEqual({
        type: 'tool_call_started',
        toolCall: { id: 'call_1', name: 'read-file', argumentsJson: '{}' },
      });
    });

    it('parses a tool call finishing', () => {
      expect(
        parseChatStreamLine(
          '{"type":"tool_call_finished","toolResult":{"toolCallId":"call_1","contentJson":"{}","isError":false}}',
        ),
      ).toEqual({
        type: 'tool_call_finished',
        toolResult: { toolCallId: 'call_1', contentJson: '{}', isError: false },
      });
    });

    it('parses an assistant message and drops a tool call that is missing fields', () => {
      expect(
        parseChatStreamLine(
          JSON.stringify({
            type: 'assistant_message',
            message: {
              role: 'assistant',
              content: 'Found it.',
              toolCalls: [
                { id: 'call_1', name: 'read-file', argumentsJson: '{}' },
                { name: 'not-a-call' },
              ],
              toolCallId: 'call_1',
            },
          }),
        ),
      ).toEqual({
        type: 'assistant_message',
        message: {
          role: 'assistant',
          content: 'Found it.',
          toolCalls: [{ id: 'call_1', name: 'read-file', argumentsJson: '{}' }],
          toolCallId: 'call_1',
        },
      });
    });

    it('parses an assistant message that has no tool calls', () => {
      expect(
        parseChatStreamLine(
          JSON.stringify({
            type: 'assistant_message',
            message: { role: 'user', content: 'hi', toolCallId: 1 },
          }),
        ),
      ).toEqual({
        type: 'assistant_message',
        message: {
          role: 'user',
          content: 'hi',
          toolCalls: undefined,
          toolCallId: undefined,
        },
      });
    });

    it('parses an assistant tool round', () => {
      expect(
        parseChatStreamLine(
          JSON.stringify({
            type: 'assistant_tool_round',
            message: { role: 'assistant', content: '', toolCalls: [] },
          }),
        ),
      ).toEqual({
        type: 'assistant_tool_round',
        message: {
          role: 'assistant',
          content: '',
          toolCalls: [],
          toolCallId: undefined,
        },
      });
    });

    it('parses an error event', () => {
      expect(parseChatStreamLine('{"type":"error","content":"The stream failed."}')).toEqual({
        type: 'error',
        content: 'The stream failed.',
      });
    });

    it('parses a done event', () => {
      expect(parseChatStreamLine('{"type":"done"}')).toEqual({ type: 'done' });
    });
  });

  describe('rejected events', () => {
    it('rejects a line that is too long', () => {
      const line = 'x'.repeat(MAX_STREAM_LINE_LENGTH + 1);
      expect(() => parseChatStreamLine(line)).toThrow(/maximum allowed length/i);
    });

    it('rejects a JSON value that is not an object', () => {
      expect(() => parseChatStreamLine('null')).toThrow(/JSON object/i);
    });

    it('rejects an event that is missing a type', () => {
      expect(() => parseChatStreamLine('{}')).toThrow(/missing a type/i);
      expect(() => parseChatStreamLine('{"type":1}')).toThrow(/missing a type/i);
    });

    it('rejects an assistant delta whose content is not a string', () => {
      expect(() => parseChatStreamLine('{"type":"assistant_delta","content":1}')).toThrow(
        /string content/i,
      );
    });

    it('rejects an error whose content is not a string', () => {
      expect(() => parseChatStreamLine('{"type":"error","content":1}')).toThrow(/string content/i);
    });

    it('rejects a tool call that is missing a required field', () => {
      expect(() =>
        parseChatStreamLine('{"type":"tool_call_started","toolCall":{"id":"call_1"}}'),
      ).toThrow(/toolCall/i);
    });

    it('rejects a tool result that is missing a required field', () => {
      expect(() =>
        parseChatStreamLine(
          '{"type":"tool_call_finished","toolResult":{"toolCallId":"call_1","contentJson":"{}"}}',
        ),
      ).toThrow(/toolResult/i);
    });

    it('rejects an assistant message that is missing content', () => {
      expect(() =>
        parseChatStreamLine('{"type":"assistant_message","message":{"role":"assistant"}}'),
      ).toThrow(/message payload/i);
    });
  });
});

describe('isTerminalStreamEventType', () => {
  it('treats done and error as terminal', () => {
    expect(isTerminalStreamEventType('done')).toBe(true);
    expect(isTerminalStreamEventType('error')).toBe(true);
    expect(isTerminalStreamEventType('assistant_delta')).toBe(false);
  });
});
