/*  Discord IRC Relay - A Discord & IRC bot that relays messages 
 *
 *  Copyright (C) 2018 Michael Flaherty // michaelwflaherty.com // michaelwflaherty@me.com
 * 
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option) 
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT 
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with 
 * this program. If not, see http://www.gnu.org/licenses/.
 */

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;

using IRCRelay.Logs;
using IRCRelay.Emoji;
using IRCRelay.LearnDB;
using IRCRelay.Commands;
using IRCRelay.Embeds;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Collections;
using Meebey.SmartIrc4net;

using System.Web;
using System.IO;

namespace IRCRelay
{
	class Discord : IDisposable
	{
		private Session session;

		private DiscordSocketClient client;
		private CommandService commands;
		private IServiceProvider services;
		private dynamic config;
		private Random random;

		/// <summary>Hourly-alarm flag toggled by the "~정각알람" command.</summary>
		public bool AlarmCall { get; set; } = true;

		public DiscordSocketClient Client { get => client; }

		public Discord(dynamic config, Session session)
		{
			this.config = config;
			this.session = session;



			var socketConfig = new DiscordSocketConfig
			{
				//WebSocketProvider = WS4NetProvider.Instance,
				LogLevel = LogSeverity.Critical
			};

			client = new DiscordSocketClient(socketConfig);
			commands = new CommandService();

			client.Log += Log;

			services = new ServiceCollection().BuildServiceProvider();

			client.MessageReceived += OnDiscordMessage;
			client.Connected += OnDiscordConnected;
			client.Disconnected += OnDiscordDisconnect;
			client.MessageUpdated += OnDiscordMsgUpdate;
			//client.ReactionAdded += OnDiscordReactionAdded;

			random = new Random();
		}

		private async Task OnDiscordReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
		}

		private async Task OnDiscordMsgUpdate(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
		{
		}

		public async Task SpawnBot()
		{
			await client.LoginAsync(TokenType.Bot, config.DiscordBotToken);
			await client.StartAsync();
		}

		public async Task OnDiscordConnected()
		{
			await Discord.Log(new LogMessage(LogSeverity.Critical, "DiscSpawn", "Discord bot initalized."));
		}

		/* When we disconnect from discord (we got booted off), we'll remake */
		public async Task OnDiscordDisconnect(Exception ex)
		{
			/* Create a new thread to kill the session. We cannot block
             * this Disconnect call */
			session.SendMessage(Session.TargetBot.Discord, "-다음장-");
			session.SendMessage(Session.TargetBot.Discord, ex.Message);
			new System.Threading.Thread(async () => { await session.Kill(Session.TargetBot.Discord); }).Start();

			await Log(new LogMessage(LogSeverity.Critical, "OnDiscordDisconnect", ex.Message));
		}

		public void Kill()
		{
			try
			{
				this.Dispose();
			}
			catch { }
		}

		public string toGif(String user, String fileurl)
		{
			try
			{
				Uri uri = new Uri(fileurl);
				string extension = Path.GetExtension(uri.AbsolutePath);
				string file = Path.GetFileName(uri.AbsolutePath);
				using (var client = new WebClient())
				{
					client.DownloadFile(fileurl, "C:\\AutoSet10\\public_html\\img\\" + file);
				}

				string new_path = "C:\\AutoSet10\\public_html\\img\\" + file.Replace(extension, ".gif");
				using (var animatedWebP = new ImageMagick.MagickImageCollection("C:\\AutoSet10\\public_html\\img\\" + file))
				{
					if (animatedWebP.Count <= 1)
					{
						return null;
					}
					else
					{
						session.SendMessage(Session.TargetBot.Discord, extension + " 변환중... (요청자:"+ user  + ", 예상 경로 : http://joy1999.codns.com:8999/img/" + file.Replace(extension, ".gif") + ")");
					}
					animatedWebP.Write(new_path, ImageMagick.MagickFormat.Gif);
				}
				return new_path;
			}
			catch (Exception e)
			{
				session.SendMessage(Session.TargetBot.Discord, "변환 몰?루");
				throw e;
			}
		}

		public async Task CheckLiveStatus()
		{
			try
			{
				List<string> channelIds = LearnDBManager.Instance.getLivesLink();

				using (HttpClient client = new HttpClient())
				{
					foreach (var channelId in channelIds)
					{
						try
						{
							string previousState = LearnDBManager.Instance.getLiveState(channelId);

							string url = $"https://api.chzzk.naver.com/polling/v2/channels/{channelId}/live-status";
							HttpResponseMessage response = await client.GetAsync(url);
							response.EnsureSuccessStatusCode();


							string responseBody = await response.Content.ReadAsStringAsync();
							JObject root = JObject.Parse(responseBody);

							// SelectToken safely returns null (instead of throwing "Cannot access
							// child value on JValue") when "content" comes back as null / a non-object.
							string status = root.SelectToken("content.status")?.ToString();
							string liveTitle = root.SelectToken("content.liveTitle")?.ToString();

							if (previousState != "OPEN" && status == "OPEN")
							{
								string info = $"방송 시작데스와: {liveTitle} (https://chzzk.naver.com/{channelId})";
								session.SendMessage(Session.TargetBot.Discord, info);
								session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);

								LearnDBManager.Instance.SaveLive(channelId, "OPEN");
							}
							else if(status == "CLOSE")
							{
								// 방송이 CLOSE로 바뀌었을 때 방송 종료 알림 추가하고 싶으면 여기서 info 보내면 돼
								if (previousState == "OPEN" && status == "CLOSE")
								{
									string info = $"방송 종료데스와: {liveTitle} (https://chzzk.naver.com/{channelId})";
									session.SendMessage(Session.TargetBot.Discord, info);
									session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
								}
								LearnDBManager.Instance.SaveLive(channelId, "CLOSE");
							}
						}
						catch (Exception ex)
						{
							if (config.IRCLogMessages == true)
							{
								LogManager.WriteLog(MsgSendType.DiscordToIRC, "channelId", "->[Exception caught]" + ex.Message, "log.txt");
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (config.IRCLogMessages == true)
				{
					LogManager.WriteLog(MsgSendType.DiscordToIRC, "CheckLiveStatus", "->[Exception caught]" + ex.Message, "log.txt");
				}
			}
		}

		public async void CallMessageAsync(String time_, String users)
		{
			Action<string> broadcast = text =>
			{
				session.SendMessage(Session.TargetBot.Discord, text);
				session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, text);
			};

			if (users.Length > 0)
			{
				var str = "지금이 " + time_ + "시라는걸 알리면서 지금이 상영회 시간이라고 말해줘.";
				await session.Ai.ChatAsync("", str, broadcast);
				broadcast(users);
			}
			else if (AlarmCall)
			{
				var str = "지금이 " + time_ + "시라는걸 알리면서 지금이 ○○ 시간이라고 말해줘. ○○은 현재 시간에 할수있는 할거리로 창의적으로 바꿔줘.";
				await session.Ai.ChatAsync("", str, broadcast);
			}
		}

		/// <summary>
		/// If the message contains a link whose start matches a registered prefix
		/// (~임베딩추가), fetch the page's Open Graph metadata and post a Discord embed.
		/// This is a fallback for links Discord doesn't auto-unfurl (e.g. dogdrip).
		/// </summary>
		private async Task TryPostLinkEmbed(SocketMessage messageParam, string content)
		{
			try
			{
				if (string.IsNullOrEmpty(content))
					return;

				Match urlMatch = Regex.Match(content, @"https?://[^\s<>()\[\]]+");
				if (!urlMatch.Success)
					return;

				string url = urlMatch.Value.TrimEnd('.', ',', ')', ']', '>', '!', '?', '"', '\'');
				if (EmbedManager.Instance.Match(url) == null)
					return;

				Embed embed = await BuildOpenGraphEmbed(url);
				if (embed != null)
					await messageParam.Channel.SendMessageAsync(embed: embed);
			}
			catch (Exception ex)
			{
				if (config.IRCLogMessages == true)
					LogManager.WriteLog(MsgSendType.DiscordToIRC, "TryPostLinkEmbed", "->[Exception caught]" + ex.Message, "log.txt");
			}
		}

		private async Task<Embed> BuildOpenGraphEmbed(string url)
		{
			string html;
			using (HttpClient http = new HttpClient())
			{
				http.Timeout = TimeSpan.FromSeconds(10);
				http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SimBibotEmbed/1.0)");
				HttpResponseMessage response = await http.GetAsync(url);
				if (!response.IsSuccessStatusCode)
					return null;
				html = await response.Content.ReadAsStringAsync();
			}

			string title = GetMetaContent(html, "og:title") ?? GetHtmlTitle(html);
			string description = GetMetaContent(html, "og:description");
			string image = GetMetaContent(html, "og:image");
			string siteName = GetMetaContent(html, "og:site_name");

			if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description) && string.IsNullOrEmpty(image))
				return null;

			var builder = new EmbedBuilder().WithUrl(url).WithColor(new Color(43, 45, 49));
			if (!string.IsNullOrEmpty(siteName))
				builder.WithAuthor(Truncate(siteName, 256));
			if (!string.IsNullOrEmpty(title))
				builder.WithTitle(Truncate(title, 256));
			if (!string.IsNullOrEmpty(description))
				builder.WithDescription(Truncate(description, 400));
			if (!string.IsNullOrEmpty(image) && (image.StartsWith("http://") || image.StartsWith("https://")))
				builder.WithImageUrl(image);

			return builder.Build();
		}

		private static string GetMetaContent(string html, string property)
		{
			string esc = Regex.Escape(property);
			// property/name attribute before content
			Match m = Regex.Match(html,
				"<meta[^>]+(?:property|name)=[\"']" + esc + "[\"'][^>]+content=[\"']([^\"']*)[\"']",
				RegexOptions.IgnoreCase);
			if (!m.Success) // content attribute before property/name
				m = Regex.Match(html,
					"<meta[^>]+content=[\"']([^\"']*)[\"'][^>]+(?:property|name)=[\"']" + esc + "[\"']",
					RegexOptions.IgnoreCase);

			return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
		}

		private static string GetHtmlTitle(string html)
		{
			Match m = Regex.Match(html, "<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase);
			return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
		}

		private static string Truncate(string value, int max)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= max)
				return value;
			return value.Substring(0, max - 1) + "…";
		}

		public async Task OnDiscordMessage(SocketMessage messageParam)
		{
			string username = "";
			string formatted = "";

			try
			{
				if (!(messageParam is SocketUserMessage message))
					return;

				if (message.Author.Id == client.CurrentUser.Id) return; // block self

				if (!messageParam.Channel.Name.Contains(config.DiscordChannelName)) return; // only relay trough specified channels
				if (messageParam.Content.Contains("__NEVER_BE_SENT_PLEASE")) return; // don't break me

				if (Program.HasMember(config, "DiscordUserIDBlacklist")) //bcompat for older configurations
				{
					/**
                     * We'll loop blacklisted user ids. If the user ID is found,
                     * then we return out and prevent the call
                     */
					foreach (string id in config.DiscordUserIDBlacklist)
					{
						if (message.Author.Id == ulong.Parse(id))
						{
							return;
						}
					}
				}

				/* Santize discord-specific notation to human readable things */
				username = (messageParam.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;
				var userid = (messageParam.Author as SocketGuildUser)?.Nickname ?? message.Author.Id.ToString();
				formatted = await DoURLMessage(messageParam.Content, message);
				formatted = MentionToNickname(formatted, message);
				formatted = EmojiToName(formatted, message);
				formatted = ChannelMentionToName(formatted, message);
				formatted = Unescape(formatted);

				// Command handling is delegated to the shared CommandDispatcher (see Commands/).
				var ctx = new IRCRelay.Commands.CommandContext(session, config, IRCRelay.Commands.CommandSource.Discord,
					username, userid, formatted, session.Ai, this, message);
				await session.Dispatcher.DispatchAsync(ctx);
				if (ctx.Stop)
					return;

				if (formatted.Length > 0 && formatted[0].ToString() == "$")
				{
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, "<@" + username + ">");
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, formatted.Replace("$", ""));
					return;
				}

				// Post a rich embed for registered link prefixes (skip command messages).
				if (!formatted.StartsWith("~"))
					await TryPostLinkEmbed(messageParam, messageParam.Content);

				if (Program.HasMember(config, "SpamFilter")) //bcompat for older configurations
				{
					foreach (string badstr in config.SpamFilter)
					{
						if (formatted.ToLower().Contains(badstr.ToLower()))
						{
							await messageParam.Channel.SendMessageAsync(messageParam.Author.Mention + ": Message with blacklisted input will not be relayed!");
							await messageParam.DeleteAsync();
							return;
						}
					}
				}

				// Send IRC Message
				if (formatted.Length > 1000)
				{
					await messageParam.Channel.SendMessageAsync(messageParam.Author.Mention + ": messages > 1000 characters cannot be successfully transmitted to IRC!");
					await messageParam.DeleteAsync();
					return;
				}

				string[] parts = formatted.Split('\n');

				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.DiscordToIRC, username, formatted, "log.txt");


				foreach (var attachment in message.Attachments)
				{
					session.SendMessage(Session.TargetBot.IRC, attachment.Url, username);
				}

				if(parts.Length < 6)
				{
					foreach (String part in parts) // we're going to send each line indpependently instead of letting irc clients handle it.
					{
						if (part.Trim().Length != 0) // if the string is not empty or just spaces
						{
							session.SendMessage(Session.TargetBot.IRC, part, username);
						}
					}
				}
				else
				{
					session.SendMessage(Session.TargetBot.IRC, "<<6줄 이상의 텍스트가 감지되었습니다.>>", username);
				}

			}
			catch (Exception e)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.DiscordToIRC, username, formatted + "->[Exception caught]" + e.ToString(), "log.txt");
			}
		}

		public static Task Log(LogMessage msg)
		{
			return Task.Run(() =>
			{
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine(msg.ToString());
			});
		}

		public void Dispose()
		{
			client.Dispose();
		}

		/**     Helper methods      **/

		public async Task<string> DoURLMessage(string input, SocketUserMessage msg)
		{
			string text = "```";
			if (input.Contains("```"))
			{
				int start = input.IndexOf(text, StringComparison.CurrentCulture);
				int end = input.IndexOf(text, start + text.Length, StringComparison.CurrentCulture);

				string code = input.Substring(start + text.Length, (end - start) - text.Length);

				if (Program.HasMember(config, "StikkedCreateUrlAndKey") && config.StikkedCreateUrlAndKey.Length > 0)
					await DoStikkedUpload(code, msg);
				else
					DoHastebinUpload(code, msg);

				input = input.Remove(start, (end - start) + text.Length);
			}
			return input;
		}

		private async Task DoStikkedUpload(string input, SocketUserMessage msg)
		{
			string[] langs = { "cpp", "csharp", "c", "java", "php" }; // we'll only do a small subset
			string language = "";
			for (int i = 0; i < langs.Length && language.Length == 0; i++)
			{
				if (input.StartsWith(langs[i]))
				{
					language = langs[i];
					input = input.Remove(0, langs[i].Length);
				}
			}

			using (var client = new HttpClient())
			{
				string username = (msg.Author as SocketGuildUser)?.Nickname ?? msg.Author.Username;
				var values = new Dictionary<string, string>
				{
					{ "name", username },
					{ "text", input.Trim() },
					{ "title", "Automated discord upload" },
					{ "lang", language }
				};
				var content = new FormUrlEncodedContent(values);
				var response = await client.PostAsync(config.StikkedCreateUrlAndKey, content); // config.StikkedCreateUrlAndKey
				var url = await response.Content.ReadAsStringAsync();

				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.DiscordToIRC, username, url, "log.txt");

				session.SendMessage(Session.TargetBot.IRC, url, username);
			}
		}

		private void DoHastebinUpload(string input, SocketUserMessage msg)
		{
			using (var client = new WebClient())
			{
				client.Headers[HttpRequestHeader.ContentType] = "text/plain";
				client.UploadDataCompleted += Hastebin_UploadCompleted;
				client.UploadDataAsync(new Uri("https://hastebin.com/documents"), null, Encoding.ASCII.GetBytes(input), msg);
			}
		}

		private void Hastebin_UploadCompleted(object sender, UploadDataCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				Log(new LogMessage(LogSeverity.Critical, "HastebinUpload", e.Error.Message));
				return;
			}
			JObject obj = JObject.Parse(Encoding.UTF8.GetString(e.Result));

			if (obj.HasValues)
			{
				string key = (string)obj["key"];
				string result = "https://hastebin.com/" + key + ".cs";

				var msg = (SocketUserMessage)e.UserState;
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.DiscordToIRC, msg.Author.Username, result, "log.txt");

				session.SendMessage(Session.TargetBot.IRC, result, msg.Author.Username);
			}
		}

		public static string MentionToNickname(string input, SocketUserMessage message)
		{
			Regex regex = new Regex("<@!?([0-9]+)>"); // create patern

			var m = regex.Matches(input); // find all matches
			var itRegex = m.GetEnumerator(); // lets iterate matches
			var itUsers = message.MentionedUsers.GetEnumerator(); // iterate mentions, too
			int difference = 0; // will explain later
			while (itUsers.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
			{
				var match = (Match)itRegex.Current; // C# makes us cast here.. gross
				var user = itUsers.Current;
				int len = match.Length;
				int start = match.Index;
				string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

				/**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
				difference += input.Length;
				string username = "@" + ((user as SocketGuildUser)?.Nickname ?? user.Username);
				input = ReplaceFirst(input, removal, username);
				difference -= input.Length;
			}

			return input;
		}

		public static string Unescape(string input)
		{
			Regex reg = new Regex("\\`[^`]*\\`");

			int count = 0;
			List<string> peices = new List<string>();
			reg.Replace(input, (m) =>
			{
				peices.Add(m.Value);
				input = input.Replace(m.Value, string.Format("__NEVER_BE_SENT_PLEASE_{0}_!@#%", count));
				count++;
				return ""; // doesn't matter what we replace with
			});

			string retstr = Regex.Replace(input, @"\\([^A-Za-z0-9])", "$1");

			// From here we prep the return string by doing our regex on the input that's not in '`'
			reg = new Regex("__NEVER_BE_SENT_PLEASE_([0-9]+)_!@#%");
			input = reg.Replace(retstr, (m) =>
			{
				return peices[int.Parse(m.Result("$1"))].ToString();
			});

			return input; // thank fuck we're done
		}

		public static string ChannelMentionToName(string input, SocketUserMessage message)
		{
			Regex regex = new Regex("<#([0-9]+)>"); // create patern

			var m = regex.Matches(input); // find all matches
			var itRegex = m.GetEnumerator(); // lets iterate matches
			var itChan = message.MentionedChannels.GetEnumerator(); // iterate mentions, too
			int difference = 0; // will explain later
			while (itChan.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
			{
				var match = (Match)itRegex.Current; // C# makes us cast here.. gross
				var channel = itChan.Current;
				int len = match.Length;
				int start = match.Index;
				string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

				/**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
				difference += input.Length;
				input = ReplaceFirst(input, removal, "#" + channel.Name);
				difference -= input.Length;
			}

			return input;
		}

		public static string ReplaceFirst(string text, string search, string replace)
		{
			int pos = text.IndexOf(search);
			if (pos < 0)
			{
				return text;
			}
			return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
		}

		// Converts <:emoji:23598052306> to :emoji:
		public static string EmojiToName(string input, SocketUserMessage message)
		{
			string returnString = input;

			Regex regex = new Regex("<[A-Za-z0-9-_]?:[A-Za-z0-9-_]+:[0-9]+>");

			for (int i = 0; i < 10; i++) //최대 이모지 10개까지만 가능(무한 루프 제거용)
			{
				Match match = regex.Match(returnString);
				if (match.Success) // contains a emoji
				{
					string substring = returnString.Substring(match.Index, match.Length);
					string[] sections = substring.Split(':');

					EmojiManager.Instance.SaveEmoji(substring, ":" + sections[1] + ":");
					returnString = returnString.Replace(substring, ":" + sections[1] + ":");
				}
				else
				{
					break;
				}
			}
			return returnString;
		}

		public void SendMessageAllToTarget(string targetGuild, string message, string targetChannel)
		{
			foreach (SocketGuild guild in Client.Guilds) // loop through each discord guild
			{
				if (guild.Name.ToLower().Contains(targetGuild.ToLower())) // find target 
				{
					SocketTextChannel channel = FindChannel(guild, targetChannel); // find desired channel

					if (channel != null) // target exists
					{
						channel.SendMessageAsync(message);
					}
				}
			}
		}


		public void SendFileAllToTarget(string targetGuild, string filepath, string targetChannel)
		{
			foreach (SocketGuild guild in Client.Guilds) // loop through each discord guild
			{
				if (guild.Name.ToLower().Contains(targetGuild.ToLower())) // find target 
				{
					SocketTextChannel channel = FindChannel(guild, targetChannel); // find desired channel

					if (channel != null) // target exists
					{
						channel.SendFileAsync(filepath,"");
					}
				}
			}
		}


		public static SocketTextChannel FindChannel(SocketGuild guild, string text)
		{
			foreach (SocketTextChannel channel in guild.TextChannels)
			{
				if (channel.Name.Contains(text))
				{
					return channel;
				}
			}

			return null;
		}
	}
}
