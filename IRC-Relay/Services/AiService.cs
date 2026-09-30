/*  Discord IRC Relay - A Discord & IRC bot that relays messages
 *
 *  Copyright (C) 2018 Michael Flaherty // michaelwflaherty.com // michaelwflaherty@me.com
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;

using IRCRelay.Emoji;
using IRCRelay.LearnAI;
using IRCRelay.Logs;

namespace IRCRelay.Services
{
	/// <summary>
	/// Wraps the OpenAI chat-completion call that powers the "~봇" command.
	/// The provider logic used to live inline in Discord.cs; it is extracted here
	/// so both the Discord and IRC command paths can share it.
	/// </summary>
	public class AiService
	{
		// Modern + inexpensive default. Override in settings.json via "AIModel"
		// (e.g. "gpt-4o-mini" is cheaper, "gpt-4.1" is pricier/stronger).
		private const string DefaultModel = "gpt-4.1-mini";

		private readonly dynamic config;
		private readonly string model = DefaultModel;
		private readonly float temperature = 0.6f;  // lower = more consistent answers
		private readonly int maxTokens = 300;        // caps output length (cost + persona's "1~2 lines")
		private readonly SearchService search;       // optional web-search tool
		private OpenAIService openAiService;

		/// <summary>True when a usable API key was configured and the client initialized.</summary>
		public bool Available => openAiService != null;

		public AiService(dynamic config)
		{
			this.config = config;
			this.search = new SearchService(config);

			try
			{
				if (Program.HasMember(config, "AIModel"))
				{
					string m = config.AIModel?.ToString();
					if (!string.IsNullOrWhiteSpace(m))
						this.model = m;
				}
				if (Program.HasMember(config, "AITemperature"))
				{
					try { this.temperature = Convert.ToSingle(config.AITemperature); } catch { }
				}
				if (Program.HasMember(config, "AIMaxTokens"))
				{
					try { this.maxTokens = Convert.ToInt32(config.AIMaxTokens); } catch { }
				}

				string apiKey = null;
				if (Program.HasMember(config, "AIApiKey"))
					apiKey = config.AIApiKey?.ToString();

				if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_OPENAI_API_KEY_HERE")
				{
					openAiService = new OpenAIService(new OpenAiOptions()
					{
						ApiKey = apiKey,
						DefaultModelId = this.model
					});
					Console.WriteLine("[AiService] enabled with model: " + this.model);
				}
				else
				{
					Console.WriteLine("[AiService] AIApiKey is not configured; AI chat is disabled.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[AiService] initialization failed: " + ex.Message);
			}
		}

		/// <summary>
		/// Sends a chat request and hands each response chunk to <paramref name="broadcast"/>
		/// (which is expected to relay the text to both Discord and IRC, mirroring the
		/// original behavior). Returns the concatenated (emoji-substituted) response text.
		/// </summary>
		public async Task<string> ChatAsync(string userName, string userMessage, Action<string> broadcast,
			IReadOnlyList<string> availableEmojis = null, IReadOnlyList<string> recentMessages = null,
			IReadOnlyList<string> imageUrls = null)
		{
			if (!Available)
			{
				broadcast("AI 키가 설정되지 않았습니다. (settings.json의 AIApiKey를 확인해주세요)");
				return "";
			}

			try
			{
				List<ChatMessage> messagesList = new List<ChatMessage>();

				if (Program.HasMember(config, "SystemContent"))
				{
					foreach (string str in config.SystemContent)
					{
						messagesList.Add(ChatMessage.FromSystem(str));
						Console.WriteLine("content : " + str);
					}
				}

				// Current date/time so "오늘 날짜" etc. isn't hallucinated.
				var kr = new CultureInfo("ko-KR");
				messagesList.Add(ChatMessage.FromSystem(
					"현재 시각은 " + DateTime.Now.ToString("yyyy년 M월 d일 dddd tt h시 m분", kr) +
					" (한국 시간)이야. 날짜/시간/요일을 물으면 반드시 이 값을 기준으로 답해."));
				
				// Recent channel mood/topics digest (refreshed periodically, cheap to inject).
				string digest = IRCRelay.Digest.DigestManager.Instance.Current;
				if (!string.IsNullOrWhiteSpace(digest))
					messagesList.Add(ChatMessage.FromSystem("요즘 이 채널 분위기/화제(참고만): " + digest));
				
				// Auto-injected Discord custom emojis (replaces hand-written emoji lists in SystemContent).
				if (availableEmojis != null && availableEmojis.Count > 0)
				{
					messagesList.Add(ChatMessage.FromSystem(
						"감정 표현에 아래 디스코드 커스텀 이모지를 이름 그대로(:이름: 형식, 대소문자 구분) 적절히 섞어 써. " +
						"기본 유니코드 이모지는 쓰지 마. 사용 가능한 이모지: " + string.Join(" ", availableEmojis)));
				}

				// A little recent-conversation context (bounded upstream to keep tokens low).
				if (recentMessages != null && recentMessages.Count > 0)
				{
					messagesList.Add(ChatMessage.FromSystem(
						"참고용 최근 채팅 맥락이야(그대로 따라 하지 말고 흐름만 참고해):\n" + string.Join("\n", recentMessages)));
				}

				if (!string.IsNullOrEmpty(userName))
				{
					messagesList.Add(ChatMessage.FromSystem("지금 너랑 대화하는 사람의 이름은 " + userName + " 이야."));
				}

				List<string> userContent = LearnAIManager.Instance.searchAllString();
				foreach (string str in userContent)
				{
					messagesList.Add(ChatMessage.FromSystem(str));
					Console.WriteLine("user content : " + str);
				}

				if (imageUrls != null && imageUrls.Count > 0)
				{
					// Multimodal message: text + image(s) for vision analysis.
					var parts = new List<MessageContent> { MessageContent.TextContent(userMessage) };
					foreach (string imageUrl in imageUrls)
						parts.Add(MessageContent.ImageUrlContent(imageUrl));
					messagesList.Add(ChatMessage.FromUser(parts));
				}
				else
				{
					messagesList.Add(ChatMessage.FromUser(userMessage));
				}

				var request = new ChatCompletionCreateRequest
				{
					Messages = messagesList,
					Model = this.model,
					Temperature = this.temperature,
					MaxTokens = this.maxTokens
				};
				if (search != null && search.Available)
					request.Tools = new List<ToolDefinition> { BuildWebSearchTool() };

				var completionResult = await RunWithToolsAsync(request, messagesList);

				if (completionResult.Successful)
				{
					string result = "";
					foreach (var choice in completionResult.Choices)
					{
						if (choice.Message?.Content == null)
						{
							throw new Exception("Choice message content is null.");
						}

						string raw = choice.Message.Content;
						string response = EmojiManager.Instance.ReplaceStringWithEmoji(raw);
						Console.WriteLine("response : " + raw);
						Console.WriteLine("after response : " + response);

						broadcast(response);
						result += raw; // return raw (":name:" form) for history/continuity
					}
					return result;
				}
				else
				{
					// Log the real API failure reason (e.g. insufficient_quota / invalid model)
					// instead of silently returning "에러데스와".
					string reason = completionResult.Error != null
						? ("code=" + completionResult.Error.Code + ", type=" + completionResult.Error.Type + ", message=" + completionResult.Error.Message)
						: "unknown (Successful=false, no Error object)";

					Console.WriteLine("[AiService] chat request failed: " + reason);
					if (config.IRCLogMessages == true)
						LogManager.WriteLog(MsgSendType.DiscordToIRC, userName ?? "", "->[AI request failed] " + reason, "log.txt");
				}
			}
			catch (Exception ex)
			{
				string errorDetails = "->[Exception caught]\n" +
									  "Message: " + ex.Message + "\n" +
									  "StackTrace: " + ex.StackTrace + "\n" +
									  "InnerException: " + (ex.InnerException?.Message ?? "None") + "\n" +
									  "Source: " + ex.Source + "\n" +
									  "TargetSite: " + ex.TargetSite;

				Console.WriteLine("Message" + errorDetails);

				if (config.IRCLogMessages == true)
				{
					LogManager.WriteLog(MsgSendType.DiscordToIRC, userName ?? "", "->[Exception caught]" + ex.Message, "log.txt");
				}
			}

			broadcast("에러데스와");
			return "에러데스와";
		}

		/// <summary>
		/// Summarizes recent chat lines into a short mood/topic digest (2~3 sentences).
		/// Used by the periodic channel-digest job; returns null on failure.
		/// </summary>
		public async Task<string> SummarizeAsync(IReadOnlyList<string> lines)
		{
			if (!Available || lines == null || lines.Count == 0)
				return null;

			try
			{
				var messages = new List<ChatMessage>
				{
					ChatMessage.FromSystem(
						"다음은 디스코드/IRC 채널의 최근 대화 로그야. 채널의 요즘 화제와 분위기를 한국어 2~3문장으로 요약해. " +
						"특정인 비방·개인정보·민감 발언 인용은 피하고, 무슨 얘기가 오가고 분위기가 어떤지 위주로 적어."),
					ChatMessage.FromUser(string.Join("\n", lines))
				};

				var res = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
				{
					Messages = messages,
					Model = this.model,
					Temperature = 0.4f,
					MaxTokens = 200
				});

				if (res.Successful && res.Choices != null && res.Choices.Count > 0)
					return res.Choices[0].Message?.Content?.Trim();

				Console.WriteLine("[AiService] summarize failed: " +
					(res.Error != null ? res.Error.Message : "unknown"));
			}
			catch (Exception ex)
			{
				Console.WriteLine("[AiService] summarize exception: " + ex.Message);
			}
			return null;
		}

		private async Task<ChatCompletionCreateResponse> RunWithToolsAsync(ChatCompletionCreateRequest request, List<ChatMessage> messages)
		{
			var res = await openAiService.ChatCompletion.CreateCompletion(request);
			int guard = 0;
			while (res.Successful && guard++ < 2)
			{
				ChatMessage m = (res.Choices != null && res.Choices.Count > 0) ? res.Choices[0].Message : null;
				if (m == null || m.ToolCalls == null || m.ToolCalls.Count == 0)
					break;

				messages.Add(m); // assistant turn that requested the tool call(s)
				foreach (var call in m.ToolCalls)
				{
					string toolResult = "검색 기능을 쓸 수 없어.";
					if (call.FunctionCall != null && call.FunctionCall.Name == "web_search" && search != null)
					{
						string q = ExtractQuery(call.FunctionCall.Arguments);
						Console.WriteLine("[web_search] " + q);
						toolResult = await search.SearchAsync(q) ?? "검색 결과가 없어.";
					}
					messages.Add(ChatMessage.FromTool(toolResult, call.Id));
				}
				request.Messages = messages;
				res = await openAiService.ChatCompletion.CreateCompletion(request);
			}
			return res;
		}

		private static ToolDefinition BuildWebSearchTool()
		{
			return ToolDefinition.DefineFunction(new FunctionDefinition
			{
				Name = "web_search",
				Description = "최신 정보(뉴스/시세/날씨/실시간 사실 등)나 확실치 않은 사실을 확인해야 할 때만 웹 검색을 한다. 일상 잡담엔 쓰지 않는다.",
				Parameters = PropertyDefinition.DefineObject(
					new Dictionary<string, PropertyDefinition>
					{
						{ "query", PropertyDefinition.DefineString("검색할 질의(한국어 가능)") }
					},
					new List<string> { "query" },
					false, null, null)
			});
		}

		private static string ExtractQuery(string arguments)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(arguments))
					return (string)JObject.Parse(arguments)["query"];
			}
			catch { }
			return "";
		}
	}
}
