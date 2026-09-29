/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace IRCRelay.Embeds
{
	/// <summary>
	/// Stores URL prefixes that the bot should build a Discord rich embed for.
	/// When Discord fails to auto-unfurl a link (e.g. dogdrip), the user registers
	/// the link's starting address here via "~임베딩추가" and the bot posts an
	/// Open Graph preview card whenever a matching link appears.
	/// Persisted to embed.json, mirroring the other *Manager singletons.
	/// </summary>
	public class EmbedManager
	{
		public static EmbedManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly EmbedManager instance = new EmbedManager();
			static Nested() { }
		}

		private readonly List<string> prefixes = new List<string>();
		private dynamic mainConfig;
		private const string file = "embed.json";

		private EmbedManager()
		{
			FileInfo fileInfo = new FileInfo(file);
			if (fileInfo.Exists)
			{
				string txt;
				using (StreamReader sw = new StreamReader(file))
				{
					txt = sw.ReadToEnd();
				}
				var readJson = JObject.Parse(txt);

				if (readJson["embeds"] != null)
				{
					foreach (var item in readJson["embeds"])
					{
						string p = item.ToString();
						if (!string.IsNullOrWhiteSpace(p) && !prefixes.Contains(p))
							prefixes.Add(p);
					}
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
			foreach (var p in prefixes)
				jarray.Add(p);
			json.Add("embeds", jarray);

			using (StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8))
			{
				sw.Write(json.ToString());
			}
		}

		/// <summary>Registers a prefix. Returns false if it was already present.</summary>
		public bool AddPrefix(string prefix)
		{
			prefix = prefix?.Trim();
			if (string.IsNullOrEmpty(prefix) || prefixes.Contains(prefix))
				return false;
			prefixes.Add(prefix);
			saveConfig();
			return true;
		}

		/// <summary>Removes a prefix. Returns false if it was not present.</summary>
		public bool RemovePrefix(string prefix)
		{
			prefix = prefix?.Trim();
			if (string.IsNullOrEmpty(prefix) || !prefixes.Remove(prefix))
				return false;
			saveConfig();
			return true;
		}

		public List<string> ListPrefixes()
		{
			return new List<string>(prefixes);
		}

		/// <summary>Returns the first registered prefix the url starts with, or null.</summary>
		public string Match(string url)
		{
			if (string.IsNullOrEmpty(url))
				return null;
			foreach (var p in prefixes)
			{
				if (url.StartsWith(p, StringComparison.OrdinalIgnoreCase))
					return p;
			}
			return null;
		}
	}
}
