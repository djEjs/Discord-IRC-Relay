﻿/*  Discord IRC Relay - A Discord & IRC bot that relays messages 
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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Meebey.SmartIrc4net;

using IRCRelay.Logs;
using IRCRelay.Emoji;
using IRCRelay.LearnDB;
using Discord;

namespace IRCRelay
{
	public class IRC
	{
		private Session session;
		private dynamic config;
		private IrcClient ircClient;
		private Random random;

		private int JoinMsgsLen;
		static private List<string> JoinMsgs = new List<string>()
		{
			" 님이 심비록 채널에 도전장을 내밀었습니다!",
			" 님이 강림하셨습니다!",
			" 님이 입장하셨습니다.",
			" 님이 소환되었습니다!",
			" 님이 땅에서 솟아났습니다!",
			" 님이 하늘에서 떨어졌습니다!",
			" 등장!! [★★★★★]",
			" 등장! [★★★★]",
			" 등장? [★★★]",
			" 등장... [☆]",

		};

		private int QuitMsgsLen;
		static private List<string> QuitMsgs = new List<string>()
		{
			" 님이 쓸쓸히 퇴장합니다...",
			" Destroyed!",
			" 님이 파괴되었습니다.",
			" 소환 해제",
			" 님이 탈주했습니다!",
			" 님이 폭★발 했습니다!",
			" 님이 하늘로 승천하고 있습니다. 그를 위해 X를 눌러 조의를 표하십시오.",
			" 님이 코어 3개로 해체 되었습니다",
		};

		public IrcClient Client { get => ircClient; set => ircClient = value; }

		public IRC(dynamic config, Session session)
		{
			this.config = config;
			this.session = session;

			ircClient = new IrcClient
			{
				Encoding = System.Text.Encoding.Default,
				SendDelay = 50,

				ActiveChannelSyncing = true,

				AutoRejoinOnKick = true
			};

			ircClient.OnConnected += OnConnected;
			ircClient.OnError += this.OnError;
			ircClient.OnChannelMessage += this.OnChannelMessage;
			ircClient.OnJoin += this.OnChannelJoin;
			ircClient.OnQuit += this.OnChannelQuit;
			ircClient.OnChannelNotice += this.OnChannelNotice;
			ircClient.OnNickChange += this.OnNickChange;

			random = new Random();
			JoinMsgsLen = JoinMsgs.Count;
			QuitMsgsLen = QuitMsgs.Count;
		}

		public void SendMessage(string username, string message)
		{
			ircClient.SendMessage(SendType.Message, config.IRCChannel, "<12@" + username + "> " + message);
		}

		public async Task SpawnBot()
		{
			await Task.Run(() =>
			{
				ircClient.Connect(config.IRCServer, config.IRCPort);

				ircClient.Login(config.IRCNick, config.IRCLoginName);

				if (config.IRCAuthString.Length != 0)
				{
					ircClient.SendMessage(SendType.Message, config.IRCAuthUser, config.IRCAuthString);

					Thread.Sleep(1000); // login delay
				}

				ircClient.RfcJoin(config.IRCChannel);
				ircClient.Listen();
			});
		}

		private void OnConnected(object sender, EventArgs e)
		{
			Discord.Log(new LogMessage(LogSeverity.Critical, "IRCSpawn", "IRC bot initalized."));
		}

		private void OnError(object sender, ErrorEventArgs e)
		{
			/* Create a new thread to kill the session. We cannot block
             * this Disconnect call */
			new Thread(async () => await session.Kill(Session.TargetBot.Both)).Start();

			Discord.Log(new LogMessage(LogSeverity.Critical, "IRCOnError", e.ErrorMessage));
		}

		private void OnNickChange(object sender, NickChangeEventArgs e)
		{
			string username = "";
			string changename = "";
			string msg = "";
			try
			{
				username = e.OldNickname;
				changename = e.NewNickname;

				string prefix = "";

				var usr = e.Data.Irc.GetChannelUser(config.IRCChannel, changename);
				if (usr.IsOp)
				{
					prefix = "@";
				}
				else if (usr.IsVoice)
				{
					prefix = "+";
				}

				if (username.Equals(this.config.IRCNick))
					return;

				session.SendMessage(Session.TargetBot.Discord, "<" + prefix + username + "> !닉 " + changename);
			}
			catch (Exception exception)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, msg + "->[Exception caught]" + exception.ToString(), "log.txt");
			}
		}
		private void OnChannelNotice(object sender, IrcEventArgs e)
		{
			string username = "";
			string msg = "";
			try
			{
				username = e.Data.Nick;
				if (username.Equals(this.config.IRCNick))
					return;

				session.SendMessage(Session.TargetBot.Discord, username + " : " + e.Data.Message);
			}
			catch (Exception exception)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, msg + "->[Exception caught]" + exception.ToString(), "log.txt");
			}
		}
		private void OnChannelJoin(object sender, IrcEventArgs e)
		{
			string username = "";
			string msg = "";
			try
			{

				username = e.Data.Nick;
				if (username.Equals(this.config.IRCNick))
					return;

				if (e.Data.Type == ReceiveType.Join && username != "^^")
				{
					session.SendMessage(Session.TargetBot.Discord, username + JoinMsgs[random.Next(JoinMsgsLen)]);
				}
			}
			catch (Exception exception)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, msg + "->[Exception caught]" + exception.ToString(), "log.txt");
			}
		}
		private void OnChannelQuit(object sender, IrcEventArgs e)
		{
			string username = "";
			string msg = "";
			try
			{
				username = e.Data.Nick;
				if (username.Equals(this.config.IRCNick))
					return;

				if (e.Data.Type == ReceiveType.Quit && username != "^^")
				{
					session.SendMessage(Session.TargetBot.Discord, username + QuitMsgs[random.Next(QuitMsgsLen)]);
				}
			}
			catch (Exception exception)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, msg + "->[Exception caught]" + exception.ToString(), "log.txt");
			}
		}
		private void OnChannelMessage(object sender, IrcEventArgs e)
		{
			string username = "";
			string msg = "";
			try
			{

				username = e.Data.Nick;
				if (username.Equals(this.config.IRCNick))
					return;

				if (Program.HasMember(config, "IRCNameBlacklist")) //bcompat for older configurations
				{
					/**
                     * We'll loop all blacklisted names, if the sender
                     * has a blacklisted name, we won't relay and ret out
                     */
					foreach (string name in config.IRCNameBlacklist)
					{
						if (username.Equals(name))
						{
							return;
						}
					}
				}

				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, e.Data.Message, "log.txt");

				msg = e.Data.Message;
				if (msg.Contains("@everyone"))
				{
					msg = msg.Replace("@everyone", "\\@everyone");
				}
				msg = EmojiToName(msg);

				string[] msg_split = msg.Split(' ');

				if (msg_split[0] == "~저장")
				{
					if (msg_split.Length > 2)
					{
						var str = "";
						for (int i = 2; i < msg_split.Length; i++)
							str += msg_split[i] + ' ';

						LearnDBManager.Instance.SaveString(msg_split[1], str);
						var saveString = "\"" + msg_split[1] + "\" 저장했습니다.";
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
						session.SendMessage(Session.TargetBot.Discord, saveString);
					}
					else
					{
						var info = "~저장 명령어 사용법 예시: **~저장 기억단어 기억할말**";
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						session.SendMessage(Session.TargetBot.Discord, info);
					}
				}
				if (msg_split[0] == "~알려")
				{
					if (msg_split.Length == 2)
					{
						string value = LearnDBManager.Instance.getString(msg_split[1]);

						if (value == null)
						{
							var saveString = "\"" + msg_split[1] + "\" 존재하지 않는 단어입니다.";
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
							session.SendMessage(Session.TargetBot.Discord, saveString);
						}
						else
						{
							var saveString = msg_split[1] + " : " + value;
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
							session.SendMessage(Session.TargetBot.Discord, saveString);

						}
					}
					else
					{
						var info = "~알려 명령어 사용법 예시: **~알려 조이**";
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						session.SendMessage(Session.TargetBot.Discord, info);
					}
				}

				if (msg_split[0] == "~로그")
				{
					string sourcePath = AppDomain.CurrentDomain.BaseDirectory + @"\log.txt";
					string targetPath = @"C:\AutoSet10\public_html\log\log.txt"; //임시로 상수로 박아봄
					System.IO.File.Copy(sourcePath, targetPath, true);
					session.SendMessage(Session.TargetBot.IRC, "http://joy1999.codns.com:8999/log/log.txt");
				}
				if (msg_split[0] == "!닉")
				{
					return;
				}
				if (msg_split[0] == "~디코")
				{
					string userList = "";

					var Guilds = session.Discord.Client.Guilds;
					foreach (var guild in Guilds)
					{
						var users = guild.Users;
						foreach (var user in users)
						{
							if (msg_split.Length > 1)
							{
								if (msg_split[1] == "all")
								{
									userList += "@" + user.Username + ", ";
								}
								else
								{
									if (user.Status != UserStatus.Offline)
									{
										userList += "@" + user.Username + ", ";
									}
								}
							}
							else
							{
								if (user.Status != UserStatus.Offline)
								{
									userList += "@" + user.Username + ", ";
								}
							}
						}
					}
					ircClient.SendMessage(SendType.Message, config.IRCChannel, userList);
				}

				if (msg_split[0] == "~골라")
				{
					if (msg_split.Length > 2)
					{
						string choose = msg_split[random.Next(1, msg_split.Length)];

						session.SendMessage(Session.TargetBot.IRC, choose);
						session.SendMessage(Session.TargetBot.Discord, choose);
					}
					else
					{
						session.SendMessage(Session.TargetBot.IRC, "[!골라] 명령어는 띄어쓰기로 구분해주세요");
					}
				}

				var Guild = session.Discord.Client.Guilds;
				foreach (var guild in Guild)
				{
					var Users = guild.Users;
					foreach (var user in Users)
					{
						msg = msg.Replace('@' + user.Username, "<@" + user.Id + ">");
					}
				}

				string prefix = "";

				var usr = e.Data.Irc.GetChannelUser(config.IRCChannel, username);
				if (usr.IsOp)
				{
					prefix = "@";
				}
				else if (usr.IsVoice)
				{
					prefix = "+";
				}

				if (Program.HasMember(config, "SpamFilter")) //bcompat for older configurations
				{
					foreach (string badstr in config.SpamFilter)
					{
						if (msg.ToLower().Contains(badstr.ToLower()))
						{
							ircClient.SendMessage(SendType.Message, config.IRCChannel, "Message with blacklisted input will not be relayed!");
							return;
						}
					}
				}

				if (msg.Length > 0 && msg[0].ToString() == "$")
				{
					session.SendMessage(Session.TargetBot.Discord, "**<" + prefix + Regex.Escape(username) + ">** ");
					session.SendMessage(Session.TargetBot.Discord, msg.Replace("$", ""));
					return;
				}

				session.SendMessage(Session.TargetBot.Discord, "**<" + prefix + Regex.Escape(username) + ">** " + msg);
			}
			catch (Exception exception)
			{
				if (config.IRCLogMessages)
					LogManager.WriteLog(MsgSendType.IRCToDiscord, username, msg + "->[Exception caught]" + exception.ToString(), "log.txt");
			}
		}

		// Converts :emoji: to <:emoji:23598052306>
		public static string EmojiToName(string input)
		{
			string returnString = input;

			Regex regex = new Regex("(?<!<[A-Za-z0-9-_]?):[A-Za-z0-9-_]+:");

			for (int i = 0; i < 10; i++) //최대 이모지 10개까지만 변환 가능(무한 루프 제거용)
			{
				Match match = regex.Match(returnString);
				if (match.Success) // contains a emoji
				{
					string substring = returnString.Substring(match.Index, match.Length);
					string replace = EmojiManager.Instance.ReplaceEmoji(substring);

					returnString = returnString.Replace(substring, replace);
				}
				else
				{
					break;
				}
			}
			return returnString;
		}
	}
}
