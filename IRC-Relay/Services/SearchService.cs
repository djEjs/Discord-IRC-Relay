/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IRCRelay.Services
{
	/// <summary>
	/// Web-search backend for the AI's "web_search" tool, using the Gemini API with
	/// Google Search grounding (generativelanguage.googleapis.com). This reuses a Gemini
	/// (Google AI Studio) API key and returns a concise, source-grounded summary.
	/// Configured via GeminiApiKey (+ optional GeminiModel) in settings.json.
	/// NOTE: the key must NOT be restricted to "Custom Search API" only — it needs access
	/// to the Generative Language API (an unrestricted AI Studio key works).
	/// </summary>
	public class SearchService
	{
		private const string DefaultModel = "gemini-flash-latest";

		private readonly string apiKey;
		private readonly string model = DefaultModel;

		public bool Available => !string.IsNullOrWhiteSpace(apiKey);

		public SearchService(dynamic config)
		{
			try
			{
				if (Program.HasMember(config, "GeminiApiKey"))
					apiKey = config.GeminiApiKey?.ToString();
				if (Program.HasMember(config, "GeminiModel"))
				{
					string m = config.GeminiModel?.ToString();
					if (!string.IsNullOrWhiteSpace(m))
						model = m.Trim();
				}
			}
			catch { }

			Console.WriteLine("[SearchService] " + (Available
				? ("enabled (Gemini grounding /" + model + ").")
				: "disabled (GeminiApiKey not set)."));
		}

		/// <summary>Returns a compact grounded summary (+ a source or two), or a short status. Never throws.</summary>
		public async Task<string> SearchAsync(string query)
		{
			if (!Available)
				return "검색 기능이 설정되지 않았어.";
			if (string.IsNullOrWhiteSpace(query))
				return "검색어가 비어있어.";

			try
			{
				string url = "https://generativelanguage.googleapis.com/v1beta/models/" + model +
					":generateContent?key=" + Uri.EscapeDataString(apiKey);

				string prompt = "다음을 웹에서 찾아 핵심만 한국어로 3~4문장 이내로 간단히 정리해줘. 최신 정보면 기준 시점도 알려줘: " + query;
				var payload = new JObject
				{
					{ "contents", new JArray { new JObject {
						{ "parts", new JArray { new JObject { { "text", prompt } } } } } } },
					{ "tools", new JArray { new JObject { { "google_search", new JObject() } } } }
				};

				using (HttpClient http = new HttpClient())
				{
					http.Timeout = TimeSpan.FromSeconds(20);
					var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
					HttpResponseMessage resp = await http.PostAsync(url, content);
					string body = await resp.Content.ReadAsStringAsync();
					if (!resp.IsSuccessStatusCode)
					{
						Console.WriteLine("[SearchService] HTTP " + (int)resp.StatusCode + ": " + body);
						return "검색 실패 (HTTP " + (int)resp.StatusCode + ")";
					}

					var json = JObject.Parse(body);
					var cand = json["candidates"]?[0];
					if (cand == null)
						return "검색 결과가 없어.";

					var sb = new StringBuilder();
					foreach (var part in cand["content"]?["parts"] as JArray ?? new JArray())
					{
						string t = (string)part["text"];
						if (!string.IsNullOrEmpty(t))
							sb.Append(t);
					}

					// Append up to 2 source links from the grounding metadata.
					var chunks = cand["groundingMetadata"]?["groundingChunks"] as JArray;
					if (chunks != null && chunks.Count > 0)
					{
						sb.Append("\n(출처: ");
						int n = 0;
						foreach (var c in chunks)
						{
							string uri = (string)c["web"]?["uri"];
							if (string.IsNullOrEmpty(uri))
								continue;
							if (n > 0)
								sb.Append(", ");
							sb.Append(uri);
							if (++n >= 2)
								break;
						}
						sb.Append(")");
					}

					string result = sb.ToString().Trim();
					return string.IsNullOrEmpty(result) ? "검색 결과가 없어." : result;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[SearchService] exception: " + ex.Message);
				return "검색 중 오류가 났어.";
			}
		}
	}
}
