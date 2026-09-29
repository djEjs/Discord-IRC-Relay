/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System.Threading.Tasks;

using IRCRelay.Emoji;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>"~콘 &lt;검색어&gt;": build a dccon.dcinside.com search link. Discord only.</summary>
	public class DcconCommand : CommandBase
	{
		private const string DCCON_SEARCH_URL = "https://dccon.dcinside.com/hot/1/title/";

		public override string[] Triggers => new[] { "~콘" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			int len = a.Length;
			if (len == 1)
			{
				ctx.SendDiscord("~콘 명령어 사용 : **~콘 간단 꼬우신** or **~콘 간단 우중콘 09 꼬우신**");
				ctx.SendDiscord(DCCON_SEARCH_URL);
				ctx.Stop = true;
				return Task.CompletedTask;
			}
			if (len == 2)
			{
				ctx.SendDiscord(DCCON_SEARCH_URL + a[1]);
				ctx.Stop = true;
				return Task.CompletedTask;
			}
			else if (len == 3)
			{
				ctx.SendDiscord(DCCON_SEARCH_URL + a[1]);
				ctx.Stop = true;
				return Task.CompletedTask;
			}
			else if (len > 3)
			{
				string str = "";
				for (int i = 1; i < len - 1; i++)
					str += a[i] + ' ';

				str = str.TrimEnd().Replace(" ", "%20");
				ctx.SendDiscord(DCCON_SEARCH_URL + str);
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~이모지숙청 &lt;단어&gt;": remove an emoji mapping. Discord only.</summary>
	public class EmojiPurgeCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~이모지숙청" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			EmojiManager.Instance.RemoveEmoji(ctx.Args[1]);
			return Task.CompletedTask;
		}
	}

	/// <summary>"~이모지 [개수]": show emoji usage statistics (max 50). Discord only.</summary>
	public class EmojiStatsCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~이모지" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			int size = 5;
			if (a.Length >= 2)
				size = int.Parse(a[1]);
			if (size >= 50)
				size = 50;

			ctx.SendDiscord(EmojiManager.Instance.printStatistics(size));
			return Task.CompletedTask;
		}
	}

	/// <summary>"~이모지초기화": reset emoji usage counts. Discord only.</summary>
	public class EmojiResetCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~이모지초기화" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			EmojiManager.Instance.InitEmojiCount();
			ctx.Broadcast("이모지 카운트를 초기화 했습니다.");
			return Task.CompletedTask;
		}
	}
}
