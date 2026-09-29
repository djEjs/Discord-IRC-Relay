/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System.Threading.Tasks;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>
	/// "~봇" / "~심심빙봇": ask the AI. Available on both Discord and IRC
	/// (IRC support is the newly-restored behavior).
	/// </summary>
	public class AiChatCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~심심빙봇", "~봇" };

		public override async Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length >= 2)
			{
				string str = "";
				for (int i = 1; i < a.Length; i++)
					str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

				await ctx.Ai.ChatAsync(ctx.Username, str, ctx.Broadcast);
			}
			else
			{
				ctx.Broadcast("~심심빙봇 명령어 사용법 예시: **~봇 죽어**");
			}
		}
	}
}
