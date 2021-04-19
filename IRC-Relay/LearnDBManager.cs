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

namespace IRCRelay.LearnDB
{
	public class LearnDBManager
	{
		public static LearnDBManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly LearnDBManager instance = new LearnDBManager();
			static Nested()
			{
			}
		}

		private Dictionary<string, string> learndbMap = new Dictionary<string, string>();
		private dynamic mainConfig;
		private const string file = "learnDB.json";

		private LearnDBManager()
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

				if (readJson["learndb"] != null)
				{
					foreach (JObject jobj in readJson["learndb"])
					{
						learndbMap.Add(jobj["key"].ToString(), jobj["value"].ToString());
					}
				}
			}
		}

		private void saveConfig()
		{
			var json = new JObject();
			var jarray = new JArray();
			foreach (var learnString in learndbMap)
			{
				var jsonChild = new JObject();
				jsonChild.Add("key", learnString.Key);
				jsonChild.Add("value", learnString.Value);
				jarray.Add(jsonChild);
			}
			json.Add("learndb", jarray);

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
			if (learndbMap.ContainsKey(key))
			{
				if (learndbMap[key] == value)
				{
					return; //이미 존재하는 이모지
				}
				else
				{
					learndbMap.Remove(key);
				}
			}
			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[SaveLearnDB] " + key + " -> " + value, "log.txt");
			learndbMap.Add(key, value);
			saveConfig();
		}

		public String getString(String key)
		{
			if (learndbMap.ContainsKey(key))
			{
				return learndbMap[key];
			}
			else
			{
				return null;
			}
		}

		public List<string> searchString(String key)
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in learndbMap)
			{
				Regex regex = new Regex(key);
				if (regex.IsMatch(item.Key))
				{
					list.Add(item.Key);
				} 
			}
			return list;
		}
	}
}