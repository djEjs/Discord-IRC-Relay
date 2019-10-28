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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Meebey.SmartIrc4net;

using IRCRelay.Logs;
using Discord;

namespace IRCRelay
{
    public class IRC
    {
        private Session session;
        private dynamic config;
        private IrcClient ircClient;
		private Random random;

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
			
			random = new Random();
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
            new Thread(async() => await session.Kill(Session.TargetBot.Both)).Start();

            Discord.Log(new LogMessage(LogSeverity.Critical, "IRCOnError", e.ErrorMessage));
        }

        private void OnChannelMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.Nick.Equals(this.config.IRCNick))
                return;

            if (Program.HasMember(config, "IRCNameBlacklist")) //bcompat for older configurations
            {
                /**
                 * We'll loop all blacklisted names, if the sender
                 * has a blacklisted name, we won't relay and ret out
                 */
                foreach (string name in config.IRCNameBlacklist)
                {
                    if (e.Data.Nick.Equals(name))
                    {
                        return;
                    }
                }
            }

            if (config.IRCLogMessages)
                LogManager.WriteLog(MsgSendType.IRCToDiscord, e.Data.Nick, e.Data.Message, "log.txt");

            string msg = e.Data.Message;
            if (msg.Contains("@everyone"))
            {
                msg = msg.Replace("@everyone", "\\@everyone");
			}

			string[] msg_split = msg.Split(' ');

			if (msg_split[0] == "!디코")
			{
				string userList = "";

				var Guilds = session.Discord.Client.Guilds;
				foreach(var guild in Guilds)
				{
					var users = guild.Users;
					foreach (var user in users)
					{
						if(msg_split.Length > 1)
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
			
			if (msg_split[0] == "!골라")
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
				foreach(var user in Users)
				{
					msg = msg.Replace('@' + user.Username, "<@" + user.Id + ">");
				}
			}

			string prefix = "";

			var usr = e.Data.Irc.GetChannelUser(config.IRCChannel, e.Data.Nick);
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

			session.SendMessage(Session.TargetBot.Discord, "**<" + prefix + Regex.Escape(e.Data.Nick) + ">** " + msg);
        }
    }
}
