using System.Text.Json;
using GrassGuy.Structs;
using Newtonsoft.Json.Linq;

namespace GrassGuy;

public class StreamHandler(StreamHandlerConfig conf, string configPath)
{
	public event Action<string, StreamResponse> StreamsGrabbed;
	private string config_path = configPath;
	private readonly UniqueQueue queue = new();
	private long twitch_token_expiry = 0;
	private StreamHandlerConfig config = conf;
	private bool isRefreshing = false;

	public async Task TwitchRefresh() 
	{
		if (twitch_token_expiry == 0) 
		{
			Console.WriteLine("Running initial validation.");
			await Task.Run(DoValidation);
		}
		else if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= twitch_token_expiry) 
		{
			Console.WriteLine("Running twitch refresh.");
			await Task.Run(DoRefresh);
		}
		await Task.Delay(TimeSpan.FromMinutes(30));
		_ = Task.Run(TwitchRefresh);
	}
	
	private async void DoValidation() 
	{
		using HttpClient client = new();
		string url = "https://id.twitch.tv/oauth2/validate";
		
		client.DefaultRequestHeaders.Add("Authorization", $"OAuth {config.oauth_token}");
		
		HttpResponseMessage response = await client.GetAsync(url);
		
		string content = await response.Content.ReadAsStringAsync();
		
		JObject json = JObject.Parse(content);
		
		if (json.GetValue("status") != null) 
		{
			Console.WriteLine("Token invalid, running refresh.");
			await Task.Run(DoRefresh);
		}
		else 
		{
			ValidTokenResponse valid = new()
			{
				client_id = (string)json.GetValue("client_id"),
				expires_in = (long)json.GetValue("expires_in"),
				user_id = (string)json.GetValue("user_id"),
				login = (string)json.GetValue("login")
			};
			if (valid.client_id != config.client_id) {
				config.client_id = valid.client_id;
				string fileContent = JsonSerializer.Serialize(config);
				File.WriteAllText(config_path, fileContent);
			}
			twitch_token_expiry = DateTimeOffset.UtcNow.AddSeconds(valid.expires_in).ToUnixTimeMilliseconds();
			Console.WriteLine($"Token valid, expiry date {DateTimeOffset.FromUnixTimeMilliseconds(twitch_token_expiry)}");
		}
	}
	
	private async void DoRefresh() 
	{
		isRefreshing = true;
		using HttpClient client = new();
		string url = "https://id.twitch.tv/oauth2/token";

		Dictionary<string, string> formData = new()
			{
				{ "grant_type", "refresh_token" },
				{ "refresh_token", config.refresh_token },
				{ "client_id", config.client_id },
				{ "client_secret", config.client_secret }
			};

		FormUrlEncodedContent content = new(formData);
		
		Console.WriteLine("Sending POST request for token refresh.");

		HttpResponseMessage response = await client.PostAsync(url, content);

		if (response.IsSuccessStatusCode)
		{
			Console.WriteLine("Successfully refreshed access token.");
			string responseData = await response.Content.ReadAsStringAsync();
			JObject responseJson = JObject.Parse(responseData);
			config.oauth_token = responseJson.GetValue("access_token").ToString();
			config.refresh_token = responseJson.GetValue("refresh_token").ToString();
			twitch_token_expiry = DateTimeOffset.UtcNow.AddSeconds((long)responseJson.GetValue("expires_in")).ToUnixTimeMilliseconds();
			Console.WriteLine($"Token type: {responseJson.GetValue("token_type")}");
			string fileContent = JsonSerializer.Serialize(config);
			File.WriteAllText(config_path, fileContent);
			Console.WriteLine($"New expiry: {DateTimeOffset.FromUnixTimeMilliseconds(twitch_token_expiry)}");
		}
		else
		{
			Console.WriteLine("Error when making request for new Twitch token.");
			string responseData = await response.Content.ReadAsStringAsync();
			JObject responseJson = JObject.Parse(responseData);
			Console.WriteLine("Response json deconstructed:");
			foreach (KeyValuePair<string, JToken?> pair in responseJson)
			{
				Console.WriteLine($"{pair.Key}: {pair.Value}");
			}
		}
		isRefreshing = false;
	}
	
	public string AddRequest(StreamGrabRequest request) 
	{
		return queue.Enqueue(request);
	}
	
	public bool HasRequestForGuild(ulong guildId) 
	{
		return queue.Contains((StreamGrabRequest item) => item.guild_id == guildId);
	}
	
	public async Task StartHandling() 
	{
		while (true) 
		{
			await Task.Delay(TimeSpan.FromMilliseconds(76));
			if (queue.Count != 0 && !isRefreshing) {
				DequeueItem dequeued = queue.Dequeue();
				await HandleGrabRequest(dequeued);
			}
		}
	}
	
	private static Dictionary<string, string> DeserializeGrabRequest(StreamGrabRequest request) 
	{
		Dictionary<string, string> dict = [];
		if (request.after != null)
			dict.Add("after", request.after);
		if (request.before != null)
			dict.Add("before", request.before);
		if (request.first != null)
			dict.Add("first", request.first.ToString());
		if (request.game_ids != null)
			dict.Add("game_ids", Utils.Join(';', request.game_ids));
		if (request.user_ids != null)
			dict.Add("user_ids", Utils.Join(';', request.user_ids));
		if (request.user_logins != null)
			dict.Add("user_logins", Utils.Join(';', request.user_logins));
		return dict;
	}
	
	private static string CreateURLParams(Dictionary<string, string> dict) 
	{
		string baseString = "?";
		
		dict.TryGetValue("game_ids", out string? game_id_list);
		dict.TryGetValue("user_ids", out string? user_id_list);
		dict.TryGetValue("user_logins", out string? user_login_list);
		dict.TryGetValue("after", out string? after);
		dict.TryGetValue("before", out string? before);
		dict.TryGetValue("first", out string? first);
		
		string[] queries = [];
		
		if (after != null)
			queries = [.. queries, $"after={after}"];
		if (before != null)
			queries = [.. queries, $"before={before}"];
		if (first != null)
			queries = [.. queries, $"first={first}"];
		if (game_id_list != null) 
		{
			string[] game_ids = game_id_list.Split(';');
			string query = "";
			foreach (string game_id in game_ids) 
			{
				query += "&";
				query += $"game_id={game_id}";
			}
			query = query.Trim('&');
			queries = [.. queries, query];
		}
		
		if (user_id_list != null) 
		{
			string[] user_ids = user_id_list.Split(';');
			string query = "";
			foreach (string user_id in user_ids) 
			{
				query += "&";
				query += $"user_id={user_id}";
			}
			query = query.Trim('&');
			queries = [.. queries, query];
		}
		if (user_login_list != null) 
		{
			string[] user_logins = user_login_list.Split(';');
			string query = "";
			foreach (string user_login in user_logins) 
			{
				query += "&";
				query += $"user_login={user_login}";
			}
			query = query.Trim('&');
			queries = [.. queries, query];
		}
		
		string queryString = string.Join("&", queries);
		
		baseString += queryString.Trim('&');
		
		return baseString;	
	}
	
	private static string DeserializeIntoParams(StreamGrabRequest request) 
	{
		Dictionary<string, string> dict = DeserializeGrabRequest(request);
		string result = CreateURLParams(dict);
		return result;
	}
	
	private static StreamResponse DeserializeResponse(string responseString) 
	{
		JObject jsonObject = JObject.Parse(responseString);
		StreamResponse response = new();
		JToken? cursor = ((JObject?)jsonObject.GetValue("pagination"))?.GetValue("cursor");
		if (cursor != null) 
			response.pagination.cursor = cursor.ToString();
		JArray? data = (JArray?)jsonObject.GetValue("data");
		if (data != null) 
		{
			Structs.Stream[] streams = [];
			foreach (JObject stream in data.Cast<JObject>()) 
			{
				Structs.Stream tstream = new()
				{
					id = stream.GetValue("id").ToString(),
					user_id = stream.GetValue("user_id").ToString(),
					user_login = stream.GetValue("user_login").ToString(),
					game_id = stream.GetValue("game_id").ToString(),
					game_name = stream.GetValue("game_name").ToString(),
					type = stream.GetValue("type").ToString(),
					title = stream.GetValue("title").ToString(),
					tags = [],
					viewer_count = (int)stream.GetValue("viewer_count"),
					started_at = stream.GetValue("started_at").ToString(),
					language = stream.GetValue("language").ToString(),
					thumbnail_url = stream.GetValue("thumbnail_url").ToString(),
					is_mature = (bool)stream.GetValue("is_mature")
				};
				JArray tags = (JArray)stream.GetValue("tags");
				foreach (JToken tag in tags) 
				{
					tstream.tags = [.. tstream.tags, tag.ToString()];
				}
				streams = [.. streams, tstream];
			}
			response.data = streams;
		}
		return response;
	}
	
	private async Task HandleGrabRequest(DequeueItem dequeued) 
	{
		using HttpClient client = new();
		client.DefaultRequestHeaders.Add("Client-ID", config.client_id);
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.oauth_token}");

		string url = $"https://api.twitch.tv/helix/streams{DeserializeIntoParams(dequeued.item)}";
		HttpResponseMessage response = await client.GetAsync(url);

		if (response.IsSuccessStatusCode)
		{
			StreamResponse streamResponse = DeserializeResponse(await response.Content.ReadAsStringAsync());
			StreamsGrabbed.Invoke(dequeued.hash, streamResponse);
		}
		else 
		{
			Console.WriteLine("Error when making request for Twitch streams.");
			string responseData = await response.Content.ReadAsStringAsync();
			JObject responseJson = JObject.Parse(responseData);
			string message = (string)responseJson.GetValue("message");
			if (message == "Client ID and OAuth token do not match") 
			{
				Console.WriteLine("Invalid Client ID/OAuth token, running refresh.");
				await Task.Run(DoRefresh);
			}
			else {
				Console.WriteLine("Response json deconstructed:");
				foreach (KeyValuePair<string, JToken?> pair in responseJson)
				{
					Console.WriteLine($"{pair.Key}: {pair.Value}");
				}
			}
		}
	}
}