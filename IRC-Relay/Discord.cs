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
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

using IRCRelay.Logs;
using IRCRelay.LearnAI;
using IRCRelay.Emoji;
using IRCRelay.LearnDB;
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
using Nito.AsyncEx;

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
		private OpenAIService openAiService;
		private bool alarmCall = true;

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

			try
			{
				openAiService = new OpenAIService(new OpenAiOptions()
				{
					ApiKey = config.AIApiKey,
					DefaultModelId = Models.Gpt_4o
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception:" + ex.Message);
			}
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

		static public string TestWebm(String webm)
		{
			Uri uri = new Uri(webm);
			string file = Path.GetFileName(uri.AbsolutePath);
			using (var client = new WebClient())
			{
				client.DownloadFile(webm, "C:\\AutoSet10\\public_html\\img\\" + file);
			}
			string new_path = "C:\\AutoSet10\\public_html\\img\\" + file.Replace(".webm", ".mp4");
			AsyncContext.Run(async () => await Xabe.FFmpeg.FFmpeg.Conversions.FromSnippet.ToMp4("C:\\AutoSet10\\public_html\\img\\" + file, new_path)).Start();

			return new_path;
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

							string status = root["content"]?["status"]?.ToString();
							string liveTitle = root["content"]?["liveTitle"]?.ToString();

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
			if(users.Length > 0)
			{
				var str = "지금이 " + time_ + "시라는걸 알리면서 지금이 상영회 시간이라고 말해줘.";
				var info = users;
				await CreateOpenAIChat("", str);
				session.SendMessage(Session.TargetBot.Discord, info);
				session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
			}
			else if(alarmCall)
			{
				var str = "지금이 " + time_ + "시라는걸 알리면서 지금이 ○○ 시간이라고 말해줘. ○○은 현재 시간에 할수있는 할거리로 창의적으로 바꿔줘.";
				await CreateOpenAIChat("", str);
			}
		}

		public async Task<string> CreateOpenAIChat(string userName, string userMessage)
		{
			try
			{
				List<ChatMessage> messagesList = new List<ChatMessage>();
				foreach (string str in config.SystemContent)
				{
					messagesList.Add(ChatMessage.FromSystem(str));
					Console.WriteLine("content : " + str);
				}

				if(userName != null && userName.Length > 0)
				{
					messagesList.Add(ChatMessage.FromSystem("지금 너랑 대화하는 사람의 이름은 " + userName + " 이야."));
				}

				List<string> userContent = LearnAIManager.Instance.searchAllString();
				foreach (string str in userContent)
				{
					messagesList.Add(ChatMessage.FromSystem(str));
					Console.WriteLine("user content : " + str);
				}

				messagesList.Add(ChatMessage.FromUser(userMessage));



				var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
				{
					Messages = messagesList,
					Model = Models.Gpt_4o
				});

				if (completionResult.Successful)
				{
					string str = "";




					foreach (var choice in completionResult.Choices)
					{
						if (choice.Message == null)
						{
							throw new Exception("Choice message is null.");
						}

						if (choice.Message?.Content == null)
						{
							throw new Exception("Choice message content is null.");
						}

						string response = choice.Message.Content;

						Console.WriteLine("response : " + response);
						Console.WriteLine("after response : " + EmojiManager.Instance.ReplaceStringWithEmoji(response));

						session.SendMessage(Session.TargetBot.Discord, EmojiManager.Instance.ReplaceStringWithEmoji(response));
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, EmojiManager.Instance.ReplaceStringWithEmoji(response));
						str += EmojiManager.Instance.ReplaceStringWithEmoji(response);
					}
					return str;
				}
			}
			catch (Exception ex)
			{
				var errorDetails = $"->[Exception caught]\n" +
								   $"Message: {ex.Message}\n" +
								   $"StackTrace: {ex.StackTrace}\n" +
								   $"InnerException: {ex.InnerException?.Message ?? "None"}\n" +
								   $"Source: {ex.Source}\n" +
								   $"TargetSite: {ex.TargetSite}";

				Console.WriteLine("Message" + errorDetails);

				if (config.IRCLogMessages == true)
				{
					LogManager.WriteLog(MsgSendType.DiscordToIRC, userName, "->[Exception caught]" + ex.Message, "log.txt");
				}
			}

			session?.SendMessage(Session.TargetBot.Discord, "에러데스와");
			session?.Irc?.Client?.SendMessage(SendType.Message, config.IRCChannel, "에러데스와");
			return "에러데스와";
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

				string[] msg_split = formatted.Split(' ');
				if (msg_split[0] == "~gif")
				{
					if (msg_split.Length > 1)
					{
						String path = toGif(username, msg_split[1]);
						if (path != null)
						{
							session.SendFile(Session.TargetBot.Discord, path);
							await messageParam.DeleteAsync();
							return;
						}
					}
					else
					{
						bool hasUploadFile = false;
						foreach (var attach in message.Attachments)
						{
							if (!attach.Filename.EndsWith(".webp"))
							{
								String path = toGif(username, attach.Url);
								if (path != null)
								{
									session.SendFile(Session.TargetBot.Discord, path);
									hasUploadFile = true;
								}
							}
						}
						if(!hasUploadFile)
						{
							var info = "사용법: ~gif \"gif변환파일주소\" 혹은 업로드시 ~gif 붙이고 업로드";
							session.SendMessage(Session.TargetBot.Discord, info);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						}
						else
						{
							await messageParam.DeleteAsync();
							return;
						}
					}
				}


				if (msg_split[0] == "~아피")
				{
					string sourcePath = AppDomain.CurrentDomain.BaseDirectory + @"\log.txt";
					string targetPath = @"C:\AutoSet10\public_html\log\log.txt"; //임시로 상수로 박아봄
					System.IO.File.Copy(sourcePath, targetPath, true);
					session.SendMessage(Session.TargetBot.Discord, "https://ip.pe.kr/");
				}
				if (msg_split[0] == "~예외")
				{
					throw new Exception("테스트용 예외");
				}
				if (msg_split[0] == "~로그")
				{
					string sourcePath = AppDomain.CurrentDomain.BaseDirectory + @"\log.txt";
					string targetPath = @"C:\AutoSet10\public_html\log\log.txt"; //임시로 상수로 박아봄
					System.IO.File.Copy(sourcePath, targetPath, true);
					session.SendMessage(Session.TargetBot.Discord, "http://joy1999.codns.com:8999/log/log.txt");
				}

				// 추가중
				if (msg_split[0] == "~콘")
				{
					string DCCON_HOME_URL = "https://dccon.dcinside.com/";
					string DCCON_SEARCH_URL = "https://dccon.dcinside.com/hot/1/title/";
					string DCCON_DETAILS_URL = "https://dccon.dcinside.com/index/package_detail";


					var len = msg_split.Length;
					if (len == 1)
					{
						var info = "~콘 명령어 사용 : **~콘 간단 꼬우신** or **~콘 간단 우중콘 09 꼬우신**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.SendMessage(Session.TargetBot.Discord, DCCON_SEARCH_URL);
						return;
					}
					if (len == 2)
					{
						session.SendMessage(Session.TargetBot.Discord, DCCON_SEARCH_URL + msg_split[1]);
						return;
					}
					// 검색어 : msg_split[1] ~ [len - 2]
					// 인덱스 : msg_split[len - 1]
					else if (len == 3)
					{
						session.SendMessage(Session.TargetBot.Discord, DCCON_SEARCH_URL + msg_split[1]);
						return;
					}
					else if (len > 3)
					{
						var str = "";

						for (int i = 1; i < len - 1; i++)
							str += msg_split[i] + ' ';

						str = str.TrimEnd().Replace(" ", "%20");

						session.SendMessage(Session.TargetBot.Discord, DCCON_SEARCH_URL + str);
					}
				}
				if (msg_split[0] == "~이모지숙청")
				{
					// 숙청할단어 : msg_split[1]
					EmojiManager.Instance.RemoveEmoji(msg_split[1]);
				}
				if (msg_split[0] == "~이모지")
				{
					int size = 5;
					if (msg_split.Length >= 2)
					{
						size = Int32.Parse(msg_split[1]);
					}
					if(size >= 50) {
						size = 50;
					}
					var str = EmojiManager.Instance.printStatistics(size);
					session.SendMessage(Session.TargetBot.Discord, str);
				}
				if (msg_split[0] == "~이모지초기화")
				{
					EmojiManager.Instance.InitEmojiCount();
					var info = "이모지 카운트를 초기화 했습니다.";
					session.SendMessage(Session.TargetBot.Discord, info);
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
				}

				if (msg_split[0] == "~저장")
				{
					if (msg_split.Length > 2)
					{
						var str = "";
						for (int i = 2; i < msg_split.Length; i++)
							str += (msg_split.Length == i + 1) ? msg_split[i] : msg_split[i] + ' ';

						LearnDBManager.Instance.SaveString(msg_split[1], str);
						var saveString = "\"" + msg_split[1] + "\" 저장했습니다.";
						session.SendMessage(Session.TargetBot.Discord, saveString);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
					}
					else
					{
						var info = "~저장 명령어 사용법 예시: **~저장 기억단어 기억할말**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}


				if (msg_split[0] == "~조련" || msg_split[0] == "~학습")
				{
					if (msg_split.Length >= 2)
					{
						var str = "";
						for (int i = 1; i < msg_split.Length; i++)
							str += (msg_split.Length == i + 1) ? msg_split[i] : msg_split[i] + ' ';

						if(str.Length > 100)
						{
							var saveString = "학습최대치 인당 100글자가 넘었습니다. 현재 " + str.Length + " 글자";

							session.SendMessage(Session.TargetBot.Discord, saveString);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
						}
						else
						{
							String past = LearnAIManager.Instance.getString(username);

							if (past != null && past.Length > 0)
							{
								var saveString = "봇에 새로운 학습 정보를 저장했습니다. 과거 학습 : " + past + "";

								session.SendMessage(Session.TargetBot.Discord, saveString);
								session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
							}
							else
							{
								var saveString = "봇에 새로운 학습 정보를 저장했습니다.";

								session.SendMessage(Session.TargetBot.Discord, saveString);
								session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
							}
							LearnAIManager.Instance.SaveString(username, str);
						}
					}
					else
					{
						String past = LearnAIManager.Instance.getString(username);
						var info = "현재 학습 : " + past;
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}
				if (msg_split[0] == "~조련목록" || msg_split[0] == "~학습목록")
				{
					var info = "현재 학습목록 \n ```";
					List<KeyValuePair<string, string>> userContent = LearnAIManager.Instance.searchAllStringPair();
					bool first_ = true;
					foreach (KeyValuePair<string, string> str in userContent)
					{
						if(!first_)
							info += "\n";
						info += str.Key + ": " + str.Value;
						first_ = false;
					}
					info += "```";
					session.SendMessage(Session.TargetBot.Discord, info);
				}

				if (msg_split[0] == "~심심빙봇" || msg_split[0] == "~봇")
				{
					if (msg_split.Length >= 2)
					{
						var str = "";
						for (int i = 1; i < msg_split.Length; i++)
							str += (msg_split.Length == i + 1) ? msg_split[i] : msg_split[i] + ' ';

						await CreateOpenAIChat(username, str);
					}
					else
					{
						var info = "~심심빙봇 명령어 사용법 예시: **~봇 죽어**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}
				if (msg_split[0] == "~알려")
				{
					if (msg_split.Length == 2)
					{
						string value = LearnDBManager.Instance.getString(msg_split[1]);

						if(value == null)
						{
							var saveString = "\"" + msg_split[1] + "\" 존재하지 않는 단어입니다.";
							session.SendMessage(Session.TargetBot.Discord, saveString);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
						} else
						{
							var saveString = msg_split[1] + " : " + value;
							session.SendMessage(Session.TargetBot.Discord, saveString);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);

						}
					}
					else
					{
						var info = "~알려 명령어 사용법 예시: **~알려 조이**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}

				if (msg_split[0] == "~찾아")
				{
					if (msg_split.Length == 2 || msg_split.Length == 3)
					{
						List<string> list = LearnDBManager.Instance.searchString(msg_split[1]);
						int check_num = 1;
						if(msg_split.Length == 3)
						{
							check_num = Int32.Parse(msg_split[2]);
							if (check_num <= 0)
								check_num = 1;
						}
						int skip = (check_num-1)*10;
						if (list.Count > 0 && list.Count > skip)
						{
							string str = "";
							int max = 10;
							int item_size = list.Count;
							foreach (String item in list)
							{
								int current = 10 * check_num + 11 - max;
								if (skip == 0)
								{
									str += item;
									if (--max <= 0)
									{
										check_num++;
										str += " (외 " + (list.Count - 10 * (check_num-1)) + "건. 다음찾기: **~찾아 " + msg_split[1] + " " + check_num + "**)";
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
							session.SendMessage(Session.TargetBot.Discord, str);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, str);
						}
						else
						{
							string info = "";
							info += msg_split[1];
							info += "-> 찾지 못하였습니다.";
							session.SendMessage(Session.TargetBot.Discord, info);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						}
					}
					else
					{
						var info = "~찾아 명령어 사용법 예시: **~찾아 뉴성군**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}



				if (msg_split[0] == "~아얄")
				{
					string nickname_list = "";
					string requested_channel = config.IRCChannel;
					Channel channel = session.Irc.Client.GetChannel(requested_channel);

					foreach (DictionaryEntry de in channel.Users)
					{
						string key = (string)de.Key;
						Meebey.SmartIrc4net.ChannelUser channeluser = (Meebey.SmartIrc4net.ChannelUser)de.Value;

						if (channeluser.Nick == config.IRCNick)
						{
							continue;
						}
						if (channeluser.IsOp)
						{
							nickname_list += "@";
						}
						if (channeluser.IsVoice)
						{
							nickname_list += "+";
						}
						nickname_list += channeluser.Nick + ", ";
					}

					session.SendMessage(Session.TargetBot.Discord, nickname_list);
				}

				if (formatted.Length > 0 && formatted[0].ToString() == "$")
				{
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, "<@" + username + ">");
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, formatted.Replace("$", ""));
					return;
				}

				if (msg_split[0] == "~상영회연장")
				{
					if (msg_split.Length == 2)
					{
						CallManager.Instance.PlusDate(msg_split[1]);
						DateTime endDate = Convert.ToDateTime(msg_split[1]);
						endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);
						string info = "상영회 연장 날짜 [";
						info += endDate.ToString();
						info += "] ~상영회참가, ~상영회탈퇴 로 참여하세요.";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
					else
					{
						var info = "~상영회연장 (종료날짜) 사용법 예시: **~상영회 9/15**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}
				if (msg_split[0] == "~상영회" || msg_split[0] == "~심심빙애니" || msg_split[0] == "~심심빙상영회")
				{
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
					} else
					{
						info += "시작일정[";
						info += startDate.ToString("yyyy-MM-dd");
						info += "] ";
					}
					session.SendMessage(Session.TargetBot.Discord, info);
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					KeyValuePair<string, string>? entry = LearnDBManager.Instance.GetLastAniEntry();
					if(entry != null)
					{
						info = "다음 상영회[";
						info += entry.Value.Key;
						info += " : ";
						info += entry.Value.Value;
						info += "] ";
						if (msg_split.Length > 2 && msg_split[1] == "추가")
						{
							msg_split[0] = "~추가";
							msg_split[1] = entry.Value.Key;
						}
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}


				if (msg_split[0] == "~추가")
				{
					if (msg_split.Length > 2)
					{
						string value = LearnDBManager.Instance.getString(msg_split[1]);

						if (value == null)
						{
							var str = "";
							for (int i = 2; i < msg_split.Length; i++)
								str += (msg_split.Length == i + 1) ? msg_split[i] : msg_split[i] + ' ';

							LearnDBManager.Instance.SaveString(msg_split[1], str);
							var saveString = "\"" + msg_split[1] + "\" 존재하지 않는 단어이므로 새로 저장했습니다.";
							session.SendMessage(Session.TargetBot.Discord, saveString);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
						}
						else
						{
							var str = value + ", ";
							for (int i = 2; i < msg_split.Length; i++)
								str += (msg_split.Length == i + 1) ? msg_split[i] : msg_split[i] + ' ';

							LearnDBManager.Instance.SaveString(msg_split[1], str);
							var saveString = "\"" + msg_split[1] + "\"에 덧붙여서 추가했습니다.";
							session.SendMessage(Session.TargetBot.Discord, saveString);
							session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);

						}
					}
					else
					{
						var info = "~추가 명령어 사용법 예시: **~추가 기억단어 추가할말**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}

				if (msg_split[0] == "~방송")
				{
					if (msg_split.Length > 1)
					{
						LearnDBManager.Instance.SaveLive(msg_split[1], "CLOSE");

						var saveString = "\"" + msg_split[1] + "\" 방송 알람을 추가했습니다.";
						session.SendMessage(Session.TargetBot.Discord, saveString);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
					}
					else
					{
						var info = "~방송 명령어 사용법 예시: **~방송 9942ff3cbf163c68e5eab624cb3acb73**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}

				if (msg_split[0] == "~방송삭제")
				{
					if (msg_split.Length > 1)
					{
						LearnDBManager.Instance.RemoveLive(msg_split[1]);

						var saveString = "\"" + msg_split[1] + "\" 방송 알람을 삭제했습니다.";
						session.SendMessage(Session.TargetBot.Discord, saveString);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, saveString);
					}
					else
					{
						var info = "~방송삭제 명령어 사용법 예시: **~방송삭제 9942ff3cbf163c68e5eab624cb3acb73**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}

				if (msg_split[0] == "~상영회시작")
				{
					if (msg_split.Length == 4 || msg_split.Length == 3)
					{
						string start_time = msg_split[2];
						string end_time = msg_split.Length == 4 ? msg_split[3] : "2099/12/31";

						CallManager.Instance.setId(msg_split[1]);
						CallManager.Instance.AddDate(start_time, end_time);
						DateTime startDate = Convert.ToDateTime(start_time);
						DateTime endDate = Convert.ToDateTime(end_time);
						startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 1, 0);
						endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);
						string info = "상영회 예정 날짜 [";
						info += startDate.ToString();
						if(msg_split.Length == 3)
						{
							info += "] -> [";
							info += endDate.ToString();
						}
						info += "] ~상영회참가, ~상영회탈퇴 로 참여하세요.";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
					else
					{
						var info = "~상영회 (시작날짜) (종료날짜) 사용법 예시: **~상영회 9/9 9/13**";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}


				if (msg_split[0] == "~정각알람")
				{
					if (alarmCall)
					{
						string info = "정각 알람 기능을 껐습니다.";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						alarmCall = false;
					}
					else
					{
						string info = "정각 알람 기능을 켰습니다.";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
						alarmCall = true;
					}
				}
				if (msg_split[0] == "~상영회참가")
				{
					CallManager.Instance.AddMember(username, userid);
					string info = "상영회 [";
					info += username;
					info += "] 참가되었습니다";
					session.SendMessage(Session.TargetBot.Discord, info);
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
				}
				if (msg_split[0] == "~상영회탈퇴" || msg_split[0] == "~상영회불참")
				{
					CallManager.Instance.RemoveMember(username);
					string info = "상영회 [";
					info += username;
					info += "] 탈퇴되었습니다";
					session.SendMessage(Session.TargetBot.Discord, info);
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
				}

				if (msg_split[0] == "~상영회종료")
				{
					CallManager.Instance.AddDate(Convert.ToDateTime(new DateTime()).ToString(), Convert.ToDateTime(new DateTime()).ToString());
					string info = "상영회가 종료되었습니다";
					session.SendMessage(Session.TargetBot.Discord, info);
					session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
				}

				if (msg_split[0] == "~상영회예외")
				{
					if (msg_split.Length == 3)
					{
						CallManager.Instance.AddExclude(msg_split[1]);
						string info = "다음 날짜엔 상영회가 없습니다. [";
						info += Convert.ToDateTime(msg_split[1]).ToString();
						info += "]";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
					else
					{
						var info = "~상영회예외 (해당날짜) 사용법 예시: **~상영회 9/10** 9월 10일은 제외함";
						session.SendMessage(Session.TargetBot.Discord, info);
						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, info);
					}
				}

				if (msg_split[0] == "~골라")
				{
					if (msg_split.Length > 2)
					{
						string choose = msg_split[random.Next(1, msg_split.Length)];

						session.Irc.Client.SendMessage(SendType.Message, config.IRCChannel, choose);
						session.SendMessage(Session.TargetBot.Discord, choose);
					}
					else
					{
						session.SendMessage(Session.TargetBot.Discord, "[!골라] 명령어는 띄어쓰기로 구분해주세요");
					}
				}

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
