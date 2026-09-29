/*  Discord IRC Relay - A Discord & IRC bot that relays messages
 *
 *  Copyright (C) 2018 Michael Flaherty // michaelwflaherty.com // michaelwflaherty@me.com
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 */

using System.Collections.Generic;
using System.Threading.Tasks;

using Meebey.SmartIrc4net;

using IRCRelay.Services;

namespace IRCRelay.Commands
{
	/// <summary>Which chat platform a command invocation arrived from.</summary>
	public enum CommandSource
	{
		Discord,
		IRC
	}

	/// <summary>
	/// Everything a command needs to run, plus the reply helpers that used to be
	/// duplicated hundreds of times as
	/// <c>session.SendMessage(Discord, x); session.Irc.Client.SendMessage(..., x);</c>.
	/// </summary>
	public class CommandContext
	{
		public Session Session { get; }
		public dynamic Config { get; }
		public CommandSource Source { get; }
		public string Username { get; }
		public string UserId { get; }
		public string Message { get; }

		/// <summary>Whitespace-split message. Commands may rewrite this to chain into another command
		/// (e.g. "~상영회 추가" rewrites Args to invoke "~추가"), mirroring the original mutable msg_split.</summary>
		public string[] Args { get; set; }

		public AiService Ai { get; }

		/// <summary>The Discord bot instance; null when the command arrived from IRC.</summary>
		internal IRCRelay.Discord DiscordBot { get; }

		/// <summary>The originating Discord message; null when the command arrived from IRC.</summary>
		public global::Discord.WebSocket.SocketUserMessage DiscordMessage { get; }

		/// <summary>Set true to stop all further command processing and message relaying
		/// (the equivalent of the original handler's early <c>return</c>).</summary>
		public bool Stop { get; set; }

		internal CommandContext(Session session, dynamic config, CommandSource source,
			string username, string userId, string message, AiService ai,
			IRCRelay.Discord discordBot = null, global::Discord.WebSocket.SocketUserMessage discordMessage = null)
		{
			Session = session;
			Config = config;
			Source = source;
			Username = username;
			UserId = userId;
			Message = message ?? "";
			Ai = ai;
			DiscordBot = discordBot;
			DiscordMessage = discordMessage;
			Args = Message.Split(' ');
		}

		public string Trigger => Args.Length > 0 ? Args[0] : "";

		public void SendDiscord(string text)
		{
			Session.SendMessage(Session.TargetBot.Discord, text);
		}

		public void SendIrc(string text)
		{
			Session.Irc?.Client?.SendMessage(SendType.Message, (string)Config.IRCChannel, text);
		}

		/// <summary>Send to both Discord and IRC (the common bot-reply pattern).</summary>
		public void Broadcast(string text)
		{
			SendDiscord(text);
			SendIrc(text);
		}

		/// <summary>Send only to the platform the command arrived from.</summary>
		public void Reply(string text)
		{
			if (Source == CommandSource.Discord)
				SendDiscord(text);
			else
				SendIrc(text);
		}
	}

	/// <summary>A chat command triggered by a keyword such as "~봇" or "~저장".</summary>
	public interface ICommand
	{
		bool CanHandle(CommandSource source);
		bool Matches(CommandContext ctx);
		Task ExecuteAsync(CommandContext ctx);
	}

	public abstract class CommandBase : ICommand
	{
		public abstract string[] Triggers { get; }

		public virtual bool SupportsDiscord => true;
		public virtual bool SupportsIrc => true;

		public virtual bool CanHandle(CommandSource source)
		{
			return source == CommandSource.Discord ? SupportsDiscord : SupportsIrc;
		}

		public virtual bool Matches(CommandContext ctx)
		{
			string trigger = ctx.Trigger;
			foreach (string t in Triggers)
			{
				if (t == trigger)
					return true;
			}
			return false;
		}

		public abstract Task ExecuteAsync(CommandContext ctx);
	}

	/// <summary>
	/// Runs registered commands in order against a context. Order matters: it preserves
	/// the original sequential if-chain semantics, including commands that rewrite
	/// <see cref="CommandContext.Args"/> to chain into a later command.
	/// </summary>
	public class CommandDispatcher
	{
		private readonly List<ICommand> commands = new List<ICommand>();

		public CommandDispatcher Register(ICommand command)
		{
			commands.Add(command);
			return this;
		}

		public async Task DispatchAsync(CommandContext ctx)
		{
			foreach (ICommand cmd in commands)
			{
				if (ctx.Stop)
					break;
				if (!cmd.CanHandle(ctx.Source))
					continue;
				if (cmd.Matches(ctx))
					await cmd.ExecuteAsync(ctx);
			}
		}
	}
}
