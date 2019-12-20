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
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

using IRCRelay.Logs;
using JsonConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace IRCRelay.Emoji
{
	public class EmojiManager
	{
		public static EmojiManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly EmojiManager instance = new EmojiManager();
			static Nested()
			{
			}
		}

		private Dictionary<string, string> emojiMap = new Dictionary<string, string>();
		private Dictionary<string, int> emojiCountMap = new Dictionary<string, int>();
		private dynamic mainConfig;
		private const string file = "emoji.json";

		private EmojiManager()
		{
			FileInfo fileInfo = new FileInfo(file);
			if (fileInfo.Exists)
			{
				String txt;
				using (StreamReader sw = new StreamReader(file))
				{
					txt = sw.ReadToEnd();
				}
				var readJson = JObject.Parse(txt);

				if (readJson["emoji"] != null)
				{
					foreach (JObject jobj in readJson["emoji"])
					{
						emojiMap.Add(jobj["key"].ToString(), jobj["value"].ToString());
						if(jobj["count"] != null) {
							emojiCountMap[jobj["value"].ToString()] = Int32.Parse(jobj["count"].ToString());
						}
					}
				}
			}
		}


		private void saveConfig()
		{
			var json = new JObject();
			var jarray = new JArray();
			foreach (var emoji in emojiMap)
			{
				var jsonChild = new JObject();
				jsonChild.Add("key", emoji.Key);
				jsonChild.Add("value", emoji.Value);
				if(emojiCountMap.ContainsKey(emoji.Value)) {
					jsonChild.Add("count", emojiCountMap[emoji.Value]);
				}
				jarray.Add(jsonChild);
			}
			json.Add("emoji", jarray);

			using (StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8))
			{
				sw.Write(json.ToString());
			}
		}

		public void setConfig(dynamic config)
		{
			this.mainConfig = config;
		}

		public void AddEmojiCount(String emojiString)
		{
			if(emojiCountMap.TryGetValue(emojiString, out int result))
			{
				emojiCountMap.Remove(emojiString);
				emojiCountMap.Add(emojiString, result+1);
			}
			else
			{
				emojiCountMap.Add(emojiString, 1);
			}
		}
		
		public void SaveEmoji(String emojiString, String simpleString)
		{
			AddEmojiCount(emojiString);
			if (emojiMap.ContainsKey(simpleString))
			{
				if (emojiMap[simpleString] == emojiString)
				{
					saveConfig();
					return; //이미 존재하는 이모지
				}
				else
				{
					emojiMap.Remove(simpleString);
				}
			}
			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[SaveEmoji] " + simpleString + " -> " + emojiString, "log.txt");
			emojiMap.Add(simpleString, emojiString);
			saveConfig();
		}

		public String ReplaceEmoji(String simpleString)
		{
			if (emojiMap.ContainsKey(simpleString))
			{
				return emojiMap[simpleString];
			}
			else
			{
				return simpleString;
			}
		}

		public string printStatistics(int size)
		{
			int i = 1;
			string returnString = "";
			var queryDesc = emojiCountMap.OrderByDescending(x => x.Value);
			foreach (var emoji in queryDesc)
			{
				for (int linecount = 0; linecount < 2; linecount++)
				{
					returnString += "**# ";
					returnString += i.ToString("D2");
					returnString += " #** ";
					returnString += emoji.Key;
					returnString += " **[";
					returnString += emoji.Value.ToString("D3");
					returnString += " 회]**        ";
					i++;
				}
				returnString += "\n";

				size--;
				if (size <= 0)
				{
					break;
				}
			}
			return returnString;
		}
	}
}