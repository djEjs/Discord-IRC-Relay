/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System.Collections.Generic;
using System.Threading.Tasks;

using IRCRelay.Embeds;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>"~임베딩추가 &lt;시작주소&gt;": register a URL prefix to auto-embed on Discord.</summary>
	public class EmbedAddCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~임베딩추가" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 2)
			{
				if (EmbedManager.Instance.AddPrefix(a[1]))
					ctx.Broadcast("임베딩 시작주소를 추가했습니다: " + a[1]);
				else
					ctx.Broadcast("이미 등록되어 있거나 잘못된 주소입니다: " + a[1]);
			}
			else
			{
				ctx.Broadcast("~임베딩추가 명령어 사용법 예시: **~임베딩추가 https://www.dogdrip.net/**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~임베딩삭제 &lt;시작주소&gt;": remove a registered URL prefix.</summary>
	public class EmbedRemoveCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~임베딩삭제" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 2)
			{
				if (EmbedManager.Instance.RemovePrefix(a[1]))
					ctx.Broadcast("임베딩 시작주소를 삭제했습니다: " + a[1]);
				else
					ctx.Broadcast("등록되어 있지 않은 주소입니다: " + a[1]);
			}
			else
			{
				ctx.Broadcast("~임베딩삭제 명령어 사용법 예시: **~임베딩삭제 https://www.dogdrip.net/**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~임베딩목록": list registered URL prefixes.</summary>
	public class EmbedListCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~임베딩목록" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			List<string> list = EmbedManager.Instance.ListPrefixes();
			if (list.Count == 0)
			{
				ctx.Broadcast("등록된 임베딩 시작주소가 없습니다.");
				return Task.CompletedTask;
			}

			string info = "현재 임베딩 시작주소 목록\n```";
			foreach (string p in list)
				info += "\n" + p;
			info += "```";
			ctx.SendDiscord(info);
			return Task.CompletedTask;
		}
	}
}
