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
	/// so token usage stays predictable.
	/// </summary>
	public class AiChatCommand : CommandBase
	{
		private const int DefaultEmojiMax = 40;
		private const int DefaultHistoryMax = 6;

		public override string[] Triggers => new[] { "~심심빙봇", "~봇" };

		public override async Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length < 2)
			{
				ctx.Broadcast("~심심빙봇 명령어 사용법 예시: **~봇 죽어**");
				return;
			}

			string str = "";
			for (int i = 1; i < a.Length; i++)
				str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

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

			string answer = await ctx.Ai.ChatAsync(ctx.Username, str, ctx.Broadcast, emojis, recent);

			// Remember this ~봇 exchange so later calls can reference prior bot conversations.
			if (historyMax > 0 && !string.IsNullOrWhiteSpace(answer)
				&& answer != "에러데스와" && answer != "AI 키가 설정되지 않았습니다. (settings.json의 AIApiKey를 확인해주세요)")
			{
				ctx.Session.RecordChat(ctx.Username, str);
				ctx.Session.RecordChat(BotLabel(ctx.Config), answer);
			}
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
