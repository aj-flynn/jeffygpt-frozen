export interface ChatMessageDto {
  role: string;
  content: string;
  toolCalls?: ToolCallDto[];
  toolCallId?: string;
}

export interface ChatRequest {
  messages: ChatMessageDto[];
}

export interface ToolCallDto {
  id: string;
  name: string;
  argumentsJson: string;
}

export interface ToolResultDto {
  toolCallId: string;
  contentJson: string;
  isError: boolean;
}

export interface ChatResponse {
  assistantMessage: ChatMessageDto;
  toolCalls: ToolCallDto[];
  toolResults: ToolResultDto[];
}

export interface AssistantDeltaEvent {
  type: 'assistant_delta';
  content: string;
}

export interface ToolCallStartedEvent {
  type: 'tool_call_started';
  toolCall: ToolCallDto;
}

export interface ToolCallFinishedEvent {
  type: 'tool_call_finished';
  toolResult: ToolResultDto;
}

export interface AssistantMessageEvent {
  type: 'assistant_message';
  message: ChatMessageDto;
}

export interface AssistantToolRoundEvent {
  type: 'assistant_tool_round';
  message: ChatMessageDto;
}

export interface StreamErrorEvent {
  type: 'error';
  content: string;
}

export interface StreamDoneEvent {
  type: 'done';
}

export type ChatStreamEvent =
  | AssistantDeltaEvent
  | ToolCallStartedEvent
  | ToolCallFinishedEvent
  | AssistantToolRoundEvent
  | AssistantMessageEvent
  | StreamErrorEvent
  | StreamDoneEvent;
