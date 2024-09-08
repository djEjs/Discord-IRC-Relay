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
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using IRCRelay.Logs;
using JsonConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace IRCRelay
{
	public class CallManager
	{
		public static CallManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly CallManager instance = new CallManager();
			static Nested()
			{
			}
		}

		private DateTime startDate = new DateTime();
		private DateTime endDate = new DateTime();
		private DateTime lastDate = new DateTime();
		private HashSet<string> callset = new HashSet<string>();
		private HashSet<DateTime> excludedate = new HashSet<DateTime>();
		private dynamic mainConfig;
		private const string file = "call.json";

		private CallManager()
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

				if (readJson["call"] != null)
				{
					foreach (JObject jobj in readJson["call"])
					{
						callset.Add(jobj["key"].ToString());
					}
				}
				if (readJson["exclude"] != null)
				{
					foreach (JObject jobj in readJson["exclude"])
					{
						excludedate.Add(Convert.ToDateTime(jobj["key"].ToString()));
					}
				}
				if (readJson["startDate"] != null)
				{
					startDate = Convert.ToDateTime(readJson["startDate"].ToString());
				}
				if (readJson["enddate"] != null)
				{
					endDate = Convert.ToDateTime(readJson["enddate"].ToString());
				}
			}
		}
		public void setConfig(dynamic config)
		{
			this.mainConfig = config;
		}

		private void saveConfig()
		{
			var json = new JObject();
			var jarray = new JArray();
			foreach (var callString in callset)
			{
				var jsonChild = new JObject();
				jsonChild.Add("key", callString);
				jsonChild.Add("value", "");
				jarray.Add(jsonChild);
			}
			json.Add("call", jarray);

			var jarray2 = new JArray();
			foreach (var dateString in excludedate)
			{
				var jsonChild = new JObject();
				jsonChild.Add("key", dateString);
				jsonChild.Add("value", "");
				jarray2.Add(jsonChild);
			}
			json.Add("exclude", jarray2);
			startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 1, 0);
			endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 0);

			json.Add("startDate", startDate.ToString());
			json.Add("enddate", endDate.ToString());

			using (StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8))
			{
				sw.Write(json.ToString());
			}
		}

		public void setConfig(dynamic config)
		{
			this.mainConfig = config;
		}

		public void AddMember(String key)
		{
			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[CallManager] add member" + key , "log.txt");
			callset.Add(key);
			saveConfig();
		}


		public void RemoveMember(String key)
		{
			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[CallManager] remove member" + key, "log.txt");
			callset.Remove(key);
			saveConfig();
		}

		public void AddDate(String startdata, String enddate)
		{
			startDate = Convert.ToDateTime(startdata);
			endDate = Convert.ToDateTime(enddate);
			lastDate = new DateTime();
			callset = new HashSet<string>();
			excludedate = new HashSet<DateTime>();

			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[CallManager] add date[" + startDate.ToString() + ", "+ endDate.ToString() + "]", "log.txt");
			saveConfig();
		}

		public void AddExclude(String data)
		{
			DateTime excludedate_ = Convert.ToDateTime(data);

			if (mainConfig.IRCLogMessages)
				LogManager.WriteLog("[CallManager] add excludedate[" + excludedate_.ToString() + "]", "log.txt");
			excludedate.Add(excludedate_);
			saveConfig();
		}

		public string GetCalls()
		{
			string results = "";

			foreach (var call in callset)
			{
				results += "@" + call + ", ";
			}
			return results;
		}
		public bool checkAble()
		{
			DateTime currentDate = DateTime.Now;

			if (lastDate.Date != currentDate.Date)
			{
				if(currentDate.Hour==1 && currentDate.Minute < 5)
				{
					if (currentDate <= endDate && currentDate >= startDate)
					{
						if (!excludedate.Contains(currentDate.Date))
						{
							lastDate = currentDate;
							return true;
						}
					}
				}

			}
			return false;
		}
	}

}