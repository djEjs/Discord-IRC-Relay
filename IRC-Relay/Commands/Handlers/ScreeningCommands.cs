/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using IRCRelay.LearnDB;

namespace IRCRelay.Commands.Handlers
{
	/// <summary>"~상영회연장 &lt;종료날짜&gt;". Discord only.</summary>
	public class ScreeningExtendCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회연장" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 2)
			{
				CallManager.Instance.PlusDate(a[1]);
				DateTime endDate = Convert.ToDateTime(a[1]);
				endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);
				ctx.Broadcast("상영회 연장 날짜 [" + endDate.ToString() + "] ~상영회참가, ~상영회탈퇴 로 참여하세요.");
			}
			else
			{
				ctx.Broadcast("~상영회연장 (종료날짜) 사용법 예시: **~상영회 9/15**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회" / "~심심빙애니" / "~심심빙상영회": show current + next screening.
	/// "~상영회 추가 &lt;내용&gt;" chains into "~추가" for the last-added anime entry. Discord only.</summary>
	public class ScreeningInfoCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회", "~심심빙애니", "~심심빙상영회" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;

			string info = "현재 상영회[";
			info += CallManager.Instance.getId();
			info += "] ";

			DateTime startDate = CallManager.Instance.getStartDate();
			DateTime endDate = CallManager.Instance.getEndDate();
			startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 1, 0);
			endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);
			if (endDate.Year < 2090)
			{
				info += "예정일정[";
				info += startDate.ToString("yyyy-MM-dd");
				info += " -> ";
				info += endDate.ToString("yyyy-MM-dd");
				info += "] ";
			}
			else
			{
				info += "시작일정[";
				info += startDate.ToString("yyyy-MM-dd");
				info += "] ";
			}
			ctx.Broadcast(info);

			KeyValuePair<string, string>? entry = LearnDBManager.Instance.GetLastAniEntry();
			if (entry != null)
			{
				info = "다음 상영회[";
				info += entry.Value.Key;
				info += " : ";
				info += entry.Value.Value;
				info += "] ";
				if (a.Length > 2 && a[1] == "추가")
				{
					a[0] = "~추가";
					a[1] = entry.Value.Key;
				}
				ctx.Broadcast(info);
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회시작 &lt;id&gt; &lt;시작날짜&gt; [종료날짜]". Discord only.</summary>
	public class ScreeningStartCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회시작" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 4 || a.Length == 3)
			{
				string start_time = a[2];
				string end_time = a.Length == 4 ? a[3] : "2099/12/31";

				CallManager.Instance.setId(a[1]);
				CallManager.Instance.AddDate(start_time, end_time);
				DateTime startDate = Convert.ToDateTime(start_time);
				DateTime endDate = Convert.ToDateTime(end_time);
				startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 1, 0);
				endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);
				string info = "상영회 예정 날짜 [";
				info += startDate.ToString();
				if (a.Length == 3)
				{
					info += "] -> [";
					info += endDate.ToString();
				}
				info += "] ~상영회참가, ~상영회탈퇴 로 참여하세요.";
				ctx.Broadcast(info);
			}
			else
			{
				ctx.Broadcast("~상영회 (시작날짜) (종료날짜) 사용법 예시: **~상영회 9/9 9/13**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~정각알람": toggle the hourly alarm flag. Discord only.</summary>
	public class AlarmToggleCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~정각알람" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			if (ctx.DiscordBot.AlarmCall)
			{
				ctx.Broadcast("정각 알람 기능을 껐습니다.");
				ctx.DiscordBot.AlarmCall = false;
			}
			else
			{
				ctx.Broadcast("정각 알람 기능을 켰습니다.");
				ctx.DiscordBot.AlarmCall = true;
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회참가". Discord only.</summary>
	public class ScreeningJoinCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회참가" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			CallManager.Instance.AddMember(ctx.Username, ctx.UserId);
			ctx.Broadcast("상영회 [" + ctx.Username + "] 참가되었습니다");
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회탈퇴" / "~상영회불참". Discord only.</summary>
	public class ScreeningLeaveCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회탈퇴", "~상영회불참" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			CallManager.Instance.RemoveMember(ctx.Username);
			ctx.Broadcast("상영회 [" + ctx.Username + "] 탈퇴되었습니다");
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회종료". Discord only.</summary>
	public class ScreeningEndCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회종료" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			CallManager.Instance.AddDate(Convert.ToDateTime(new DateTime()).ToString(), Convert.ToDateTime(new DateTime()).ToString());
			ctx.Broadcast("상영회가 종료되었습니다");
			return Task.CompletedTask;
		}
	}

	/// <summary>"~상영회예외 &lt;날짜&gt;". Discord only.</summary>
	public class ScreeningExcludeCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~상영회예외" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length == 3)
			{
				CallManager.Instance.AddExclude(a[1]);
				ctx.Broadcast("다음 날짜엔 상영회가 없습니다. [" + Convert.ToDateTime(a[1]).ToString() + "]");
			}
			else
			{
				ctx.Broadcast("~상영회예외 (해당날짜) 사용법 예시: **~상영회 9/10** 9월 10일은 제외함");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~방송 &lt;channelId&gt;": register a chzzk live alarm. Discord only.</summary>
	public class LiveAddCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~방송" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length > 1)
			{
				LearnDBManager.Instance.SaveLive(a[1], "CLOSE");
				ctx.Broadcast("\"" + a[1] + "\" 방송 알람을 추가했습니다.");
			}
			else
			{
				ctx.Broadcast("~방송 명령어 사용법 예시: **~방송 9942ff3cbf163c68e5eab624cb3acb73**");
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>"~방송삭제 &lt;channelId&gt;". Discord only.</summary>
	public class LiveRemoveCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~방송삭제" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length > 1)
			{
				LearnDBManager.Instance.RemoveLive(a[1]);
				ctx.Broadcast("\"" + a[1] + "\" 방송 알람을 삭제했습니다.");
			}
			else
			{
				ctx.Broadcast("~방송삭제 명령어 사용법 예시: **~방송삭제 9942ff3cbf163c68e5eab624cb3acb73**");
			}
			return Task.CompletedTask;
		}
	}
}
