/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.IO;
using System.Text;

using Newtonsoft.Json.Linq;

namespace IRCRelay.Digest
{
	/// <summary>
	/// Holds a short, periodically-refreshed summary of the channel's recent mood/topics.
	/// Generated once per interval (see Session) from a rolling message buffer, then injected
	/// into the AI prompt so "~봇" is aware of what's going on without re-sending full history.
	/// Persisted to digest.json so it survives restarts.
	/// </summary>
	public class DigestManager
	{
		public static DigestManager Instance { get { return Nested.instance; } }

		private class Nested
		{
			internal static readonly DigestManager instance = new DigestManager();
			static Nested() { }
		}

		private string current = "";
		private DateTime updatedUtc = DateTime.MinValue;
		private const string file = "digest.json";

		private DigestManager()
		{
			try
			{
				if (new FileInfo(file).Exists)
				{
					string txt;
					using (StreamReader sw = new StreamReader(file))
						txt = sw.ReadToEnd();
					var json = JObject.Parse(txt);
					current = (string)json["digest"] ?? "";
					if (json["updatedUtc"] != null &&
						DateTime.TryParse((string)json["updatedUtc"], null,
							System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t))
						updatedUtc = t;
				}
			}
			catch { }
		}

		public string Current { get { return current; } }
		public DateTime UpdatedUtc { get { return updatedUtc; } }

		public void Set(string text)
		{
			current = text ?? "";
			updatedUtc = DateTime.UtcNow;
			try
			{
				var json = new JObject
				{
					{ "digest", current },
					{ "updatedUtc", updatedUtc.ToString("o") }
				};
				using (StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8))
					sw.Write(json.ToString());
			}
			catch { }
		}
	}
}
