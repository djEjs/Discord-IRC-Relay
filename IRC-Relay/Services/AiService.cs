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
using System.Threading.Tasks;

using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

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
		private OpenAIService openAiService;

		/// <summary>True when a usable API key was configured and the client initialized.</summary>
		public bool Available => openAiService != null;

		public AiService(dynamic config)
		{
			this.config = config;

			try
			{
				if (Program.HasMember(config, "AIModel"))
				{
					string m = config.AIModel?.ToString();
					if (!string.IsNullOrWhiteSpace(m))
						this.model = m;
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
		public async Task<string> ChatAsync(string userName, string userMessage, Action<string> broadcast)
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

				messagesList.Add(ChatMessage.FromUser(userMessage));

				var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
				{
					Messages = messagesList,
					Model = this.model
				});

				if (completionResult.Successful)
				{
					string result = "";
					foreach (var choice in completionResult.Choices)
					{
						if (choice.Message?.Content == null)
						{
							throw new Exception("Choice message content is null.");
						}

						string response = EmojiManager.Instance.ReplaceStringWithEmoji(choice.Message.Content);
						Console.WriteLine("response : " + choice.Message.Content);
						Console.WriteLine("after response : " + response);

						broadcast(response);
						result += response;
					}
					return result;
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
	}
}
