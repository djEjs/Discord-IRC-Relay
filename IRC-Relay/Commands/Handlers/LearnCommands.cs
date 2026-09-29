/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System.Collections.Generic;
using System.Threading.Tasks;

using IRCRelay.LearnDB;
using IRCRelay.LearnAI;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>"~저장 &lt;단어&gt; &lt;내용&gt;": store a keyword in the word DB. Both platforms.</summary>
	public class SaveCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~저장" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length > 2)
			{
				string str = "";
				for (int i = 2; i < a.Length; i++)
					str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

				LearnDBManager.Instance.SaveString(a[1], str);
				ctx.Broadcast("\"" + a[1] + "\" 저장했습니다.");
			}
			else
			{
				ctx.Broadcast("~저장 명령어 사용법 예시: **~저장 기억단어 기억할말**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~알려 &lt;단어&gt;": look up a keyword in the word DB. Both platforms.</summary>
	public class TellCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~알려" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 2)
			{
				string value = LearnDBManager.Instance.getString(a[1]);
				if (value == null)
					ctx.Broadcast("\"" + a[1] + "\" 존재하지 않는 단어입니다.");
				else
					ctx.Broadcast(a[1] + " : " + value);
			}
			else
			{
				ctx.Broadcast("~알려 명령어 사용법 예시: **~알려 조이**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~찾아 &lt;단어&gt; [페이지]": search the word DB (10 per page). Discord only.</summary>
	public class FindCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~찾아" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 2 || a.Length == 3)
			{
				List<string> list = LearnDBManager.Instance.searchString(a[1]);
				int check_num = 1;
				if (a.Length == 3)
				{
					check_num = int.Parse(a[2]);
					if (check_num <= 0)
						check_num = 1;
				}
				int skip = (check_num - 1) * 10;
				if (list.Count > 0 && list.Count > skip)
				{
					string str = "";
					int max = 10;
					int item_size = list.Count;
					foreach (string item in list)
					{
						int current = 10 * check_num + 11 - max;
						if (skip == 0)
						{
							str += item;
							if (--max <= 0)
							{
								check_num++;
								str += " (외 " + (list.Count - 10 * (check_num - 1)) + "건. 다음찾기: **~찾아 " + a[1] + " " + check_num + "**)";
								break;
							}
							else if (current != item_size)
							{
								str += ", ";
							}
						}
						else
						{
							skip--;
						}
					}
					ctx.Broadcast(str);
				}
				else
				{
					ctx.Broadcast(a[1] + "-> 찾지 못하였습니다.");
				}
			}
			else
			{
				ctx.Broadcast("~찾아 명령어 사용법 예시: **~찾아 뉴성군**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~추가 &lt;단어&gt; &lt;내용&gt;": append to an existing keyword (or create it). Discord only.</summary>
	public class AppendCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~추가" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length > 2)
			{
				string value = LearnDBManager.Instance.getString(a[1]);
				if (value == null)
				{
					string str = "";
					for (int i = 2; i < a.Length; i++)
						str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

					LearnDBManager.Instance.SaveString(a[1], str);
					ctx.Broadcast("\"" + a[1] + "\" 존재하지 않는 단어이므로 새로 저장했습니다.");
				}
				else
				{
					string str = value + ", ";
					for (int i = 2; i < a.Length; i++)
						str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

					LearnDBManager.Instance.SaveString(a[1], str);
					ctx.Broadcast("\"" + a[1] + "\"에 덧붙여서 추가했습니다.");
				}
			}
			else
			{
				ctx.Broadcast("~추가 명령어 사용법 예시: **~추가 기억단어 추가할말**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~조련" / "~학습": teach the AI a per-user fact (max 100 chars). Discord only.</summary>
	public class TrainCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~조련", "~학습" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length >= 2)
			{
				string str = "";
				for (int i = 1; i < a.Length; i++)
					str += (a.Length == i + 1) ? a[i] : a[i] + ' ';

				if (str.Length > 100)
				{
					ctx.Broadcast("학습최대치 인당 100글자가 넘었습니다. 현재 " + str.Length + " 글자");
				}
				else
				{
					string past = LearnAIManager.Instance.getString(ctx.Username);
					if (past != null && past.Length > 0)
						ctx.Broadcast("봇에 새로운 학습 정보를 저장했습니다. 과거 학습 : " + past + "");
					else
						ctx.Broadcast("봇에 새로운 학습 정보를 저장했습니다.");

					LearnAIManager.Instance.SaveString(ctx.Username, str);
				}
			}
			else
			{
				string past = LearnAIManager.Instance.getString(ctx.Username);
				ctx.Broadcast("현재 학습 : " + past);
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~조련목록" / "~학습목록": list all AI-learned facts. Discord only (output to Discord).</summary>
	public class TrainListCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~조련목록", "~학습목록" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string info = "현재 학습목록 \n ```";
			List<KeyValuePair<string, string>> userContent = LearnAIManager.Instance.searchAllStringPair();
			bool first_ = true;
			foreach (KeyValuePair<string, string> str in userContent)
			{
				if (!first_)
					info += "\n";
				info += str.Key + ": " + str.Value;
				first_ = false;
			}
			info += "```";
			ctx.SendDiscord(info);
			return Task.CompletedTask;
		}
	}
}
