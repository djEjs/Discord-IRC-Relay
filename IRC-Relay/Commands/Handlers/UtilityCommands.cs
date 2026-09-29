/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.Collections;
using System.Threading.Tasks;

using Discord;                 // UserStatus
using Meebey.SmartIrc4net;     // Channel, ChannelUser

namespace IRCRelay.Commands.Handlers
{
	/// <summary>"~gif &lt;url&gt;" or "~gif" + attachment: convert to gif. Discord only.</summary>
	public class GifCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~gif" };
		public override bool SupportsIrc => false;

		public override async Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length > 1)
			{
				string path = ctx.DiscordBot.toGif(ctx.Username, a[1]);
				if (path != null)
				{
					ctx.Session.SendFile(Session.TargetBot.Discord, path);
					await ctx.DiscordMessage.DeleteAsync();
					ctx.Stop = true;
					return;
				}
			}
			else
			{
				bool hasUploadFile = false;
				foreach (var attach in ctx.DiscordMessage.Attachments)
				{
					if (!attach.Filename.EndsWith(".webp"))
					{
						string path = ctx.DiscordBot.toGif(ctx.Username, attach.Url);
						if (path != null)
						{
							ctx.Session.SendFile(Session.TargetBot.Discord, path);
							hasUploadFile = true;
						}
					}
				}
				if (!hasUploadFile)
				{
					ctx.Broadcast("사용법: ~gif \"gif변환파일주소\" 혹은 업로드시 ~gif 붙이고 업로드");
				}
				else
				{
					await ctx.DiscordMessage.DeleteAsync();
					ctx.Stop = true;
				}
			}
		}
	}

	/// <summary>"~아피": copy the log to the web dir and post ip.pe.kr. Discord only.</summary>
	public class ApiCopyCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~아피" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string sourcePath = AppDomain.CurrentDomain.BaseDirectory + @"\log.txt";
			string targetPath = @"C:\AutoSet10\public_html\log\log.txt"; //임시로 상수로 박아봄
			System.IO.File.Copy(sourcePath, targetPath, true);
			ctx.SendDiscord("https://ip.pe.kr/");
			return Task.CompletedTask;
		}
	}

	/// <summary>"~로그": copy the log to the web dir and post its URL to the originating platform.</summary>
	public class LogCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~로그" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string sourcePath = AppDomain.CurrentDomain.BaseDirectory + @"\log.txt";
			string targetPath = @"C:\AutoSet10\public_html\log\log.txt"; //임시로 상수로 박아봄
			System.IO.File.Copy(sourcePath, targetPath, true);

			const string url = "http://joy1999.codns.com:8999/log/log.txt";
			// Preserve original per-platform send (IRC path went through Session.SendMessage,
			// which prefixes "<12@> ").
			if (ctx.Source == CommandSource.Discord)
				ctx.SendDiscord(url);
			else
				ctx.Session.SendMessage(Session.TargetBot.IRC, url);
			return Task.CompletedTask;
		}
	}

	/// <summary>"~아얄": list IRC channel users. Discord only (output to Discord).</summary>
	public class IrcUserListCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~아얄" };
		public override bool SupportsIrc => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string nickname_list = "";
			string requested_channel = ctx.Config.IRCChannel;
			Channel channel = ctx.Session.Irc.Client.GetChannel(requested_channel);

			foreach (DictionaryEntry de in channel.Users)
			{
				ChannelUser channeluser = (ChannelUser)de.Value;

				if (channeluser.Nick == ctx.Config.IRCNick)
					continue;
				if (channeluser.IsOp)
					nickname_list += "@";
				if (channeluser.IsVoice)
					nickname_list += "+";
				nickname_list += channeluser.Nick + ", ";
			}

			ctx.SendDiscord(nickname_list);
			return Task.CompletedTask;
		}
	}

	/// <summary>"~디코 [all]": list Discord users. IRC only (output to IRC).</summary>
	public class DiscordUserListCommand : CommandBase
	{
		public override string[] Triggers => new[] { "~디코" };
		public override bool SupportsDiscord => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			string userList = "";

			var guilds = ctx.Session.Discord.Client.Guilds;
			foreach (var guild in guilds)
			{
				foreach (var user in guild.Users)
				{
					if (a.Length > 1)
					{
						if (a[1] == "all")
						{
							userList += "@" + user.Username + ", ";
						}
						else if (user.Status != UserStatus.Offline)
						{
							userList += "@" + user.Username + ", ";
						}
					}
					else if (user.Status != UserStatus.Offline)
					{
						userList += "@" + user.Username + ", ";
					}
				}
			}
			ctx.SendIrc(userList);
			return Task.CompletedTask;
		}
	}

	/// <summary>"!닉" typed by a user on IRC is swallowed (it is the nick-change relay marker). IRC only.</summary>
	public class NickMarkerCommand : CommandBase
	{
		public override string[] Triggers => new[] { "!닉" };
		public override bool SupportsDiscord => false;

		public override Task ExecuteAsync(CommandContext ctx)
		{
			ctx.Stop = true;
			return Task.CompletedTask;
		}
	}

	/// <summary>"~골라 &lt;a&gt; &lt;b&gt; ...": pick one at random. Both platforms.</summary>
	public class ChooseCommand : CommandBase
	{
		private readonly Random random = new Random();

		public override string[] Triggers => new[] { "~골라" };

		public override Task ExecuteAsync(CommandContext ctx)
		{
			string[] a = ctx.Args;
			if (a.Length >= 2)
			{
				// Allow a single option too (random.Next(1, 2) always returns 1).
				string choose = a[random.Next(1, a.Length)];
				ctx.Broadcast(choose);
			}
			else
			{
				ctx.Reply("[!골라] 사용법 예시: **~골라 짜장 짬뽕** (한 개도 가능)");
			}
			return Task.CompletedTask;
		}
	}
}
