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
using System.Text.RegularExpressions;

using IRCRelay.Logs;
using JsonConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;

namespace IRCRelay.LearnAI
{
	public class LearnAIManager
	{
		public static LearnAIManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly LearnAIManager instance = new LearnAIManager();
			static Nested()
			{
			}
		}

		private Dictionary<string, string> learnaiMap = new Dictionary<string, string>();
		private dynamic mainConfig;
		private const string file = "learnAI.json";

		private LearnAIManager()
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

				if (readJson["learnai"] != null)
				{
					foreach (JObject jobj in readJson["learnai"])
					{
						learnaiMap.Add(jobj["key"].ToString(), jobj["value"].ToString());
					}
				}
			}
		}

		private void saveConfig()
		{
			var json = new JObject();
			var jarray = new JArray();
			foreach (var learnString in learnaiMap)
			{
				var jsonChild = new JObject();
				jsonChild.Add("key", learnString.Key);
				jsonChild.Add("value", learnString.Value);
				jarray.Add(jsonChild);
			}
			json.Add("learnai", jarray);

			using (StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8))
			{
				sw.Write(json.ToString());
			}
		}

		public void setConfig(dynamic config)
		{
			this.mainConfig = config;
		}

		public void SaveString(String key, String value)
		{
			if (learnaiMap.ContainsKey(key))
			{
				if (learnaiMap[key] == value)
				{
					return;
				}
				else
				{
					learnaiMap.Remove(key);
				}
			}
			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[SaveLearnAI] " + key + " -> " + value, "log.txt");
			learnaiMap.Add(key, value);
			saveConfig();
		}

		public String getString(String key)
		{
			if (learnaiMap.ContainsKey(key))
			{
				return learnaiMap[key];
			}
			else
			{
				return null;
			}
		}
		public List<string> searchAllString()
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in learnaiMap)
			{
				list.Add(item.Value);
			}
			return list;
		}
	}
}