/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System.Collections.Generic;

namespace IRCRelay
{
	/// <summary>
	/// A small in-memory ring buffer of recent channel messages (both Discord and IRC),
	/// used to give the AI ("~봇") a bit of conversation context without extra API calls.
	/// Bounded in both count and per-message length to keep token usage predictable.
	/// </summary>
	public class ConversationHistory
	{
		private readonly int capacity;
		private readonly int maxChars;
		private readonly LinkedList<string> items = new LinkedList<string>();
		private readonly object gate = new object();

		public ConversationHistory(int capacity = 20, int maxChars = 200)
		{
			this.capacity = capacity < 1 ? 1 : capacity;
			this.maxChars = maxChars < 20 ? 20 : maxChars;
		}

		public void Add(string user, string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return;

			text = text.Replace("\n", " ").Trim();
			if (text.Length > maxChars)
				text = text.Substring(0, maxChars) + "…";

			string line = (string.IsNullOrEmpty(user) ? "" : user + ": ") + text;

			lock (gate)
			{
				items.AddLast(line);
				while (items.Count > capacity)
					items.RemoveFirst();
			}
		}

		/// <summary>Returns up to <paramref name="n"/> most-recent lines, oldest first.</summary>
		public List<string> GetRecent(int n)
		{
			if (n < 1)
				return new List<string>();

			lock (gate)
			{
				var all = new List<string>(items);
				int start = all.Count - n;
				if (start < 0)
					start = 0;
				return all.GetRange(start, all.Count - start);
			}
		}
	}
}
