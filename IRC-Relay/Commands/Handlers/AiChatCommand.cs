/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using IRCRelay.Emoji;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>
	/// "~봇" / "~심심빙봇": ask the AI. Available on both Discord and IRC.
	/// Enriches the prompt with the channel's frequently-used custom emojis and a
	/// little recent-conversation context, both bounded (via AIEmojiMax / AIHistoryMax)
	/// so token usage stays predictable. On Discord, image attachments are sent to the
	/// vision model so "~봇 이거 뭐야?" + an image analyzes the picture.
	/// </summary>
	public class AiChatCommand : CommandBase
	{
		private const int DefaultEmojiMax = 40;
		private const int DefaultHistoryMax = 6;

		public override string[] Triggers => new[] { "~심심빙봇", "~봇" };

		public override async Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;

			// Collect image attachment URLs (Discord only) for vision analysis.
			List<string> images = null;
			if (ctx.DiscordMessage != null && ctx.DiscordMessage.Attachments != null)
			{
				foreach (var att in ctx.DiscordMessage.Attachments)
				{
					if (IsImage(att.ContentType, att.Filename))
						(images ?? (images = new List<string>())).Add(att.Url);
				}
			}
			bool hasImage = images != null && images.Count > 0;

			if (a.Length < 2 && !hasImage)
			{
				ctx.Broadcast("~심심빙봇 명령어 사용법 예시: **~봇 죽어** (이미지를 첨부하고 ~봇 하면 이미지 분석도 돼요)");
				return;
			}

			string str = "";
			for (int i = 1; i < a.Length; i++)
				str += (a.Length == i + 1) ? a[i] : a[i] + ' ';
			if (string.IsNullOrWhiteSpace(str) && hasImage)
				str = "이 이미지에 대해 이야기해줘.";

			int emojiMax = ConfigInt(ctx.Config, "AIEmojiMax", DefaultEmojiMax);
			int historyMax = ConfigInt(ctx.Config, "AIHistoryMax", DefaultHistoryMax);

			// Prefer the guild's real custom emojis; fall back to ones the bot has seen.
			IReadOnlyList<string> emojis = null;
			if (emojiMax > 0)
			{
				List<string> guildEmojis = ctx.Session.Discord?.GetGuildEmojiNames(emojiMax);
				emojis = (guildEmojis != null && guildEmojis.Count > 0)
					? guildEmojis
					: EmojiManager.Instance.GetTopEmojiNames(emojiMax);
			}

			IReadOnlyList<string> recent = historyMax > 0 ? ctx.Session.History.GetRecent(historyMax) : null;

			string answer = await ctx.Ai.ChatAsync(ctx.Username, str, ctx.Broadcast, emojis, recent, images);

			// Remember this ~봇 exchange so later calls can reference prior bot conversations.
			if (historyMax > 0 && !string.IsNullOrWhiteSpace(answer)
				&& answer != "에러데스와" && answer != "AI 키가 설정되지 않았습니다. (settings.json의 AIApiKey를 확인해주세요)")
			{
				ctx.Session.RecordChat(ctx.Username, hasImage ? "[이미지] " + str : str);
				ctx.Session.RecordChat(BotLabel(ctx.Config), answer);
			}
		}

		private static bool IsImage(string contentType, string filename)
		{
			if (!string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/"))
				return true;
			string f = (filename ?? "").ToLowerInvariant();
			return f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg")
				|| f.EndsWith(".gif") || f.EndsWith(".webp");
		}

		private static string BotLabel(dynamic config)
		{
			try
			{
				if (Program.HasMember(config, "IRCNick"))
				{
					string n = config.IRCNick?.ToString();
					if (!string.IsNullOrWhiteSpace(n))
						return n;
				}
			}
			catch { }
			return "심심빙봇";
		}

		private static int ConfigInt(dynamic config, string name, int fallback)
		{
			try
			{
				if (Program.HasMember(config, name))
					return Convert.ToInt32(config[name]);
			}
			catch { }
			return fallback;
		}
	}
}
