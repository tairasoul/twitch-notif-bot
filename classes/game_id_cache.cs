using GrassGuy.Structs;
using LinqToDB;
using LinqToDB.Mapping;
using Newtonsoft.Json.Linq;

namespace GrassGuy;

public class GameNameIdCache(GuildConfigHandler conf, StreamHandlerConfig streamConfig)
{
	private ContextWrapper context = conf.wrapper;
	private StreamHandlerConfig config = streamConfig;
	
	public async Task<string[]?> ParseGameNames(string[]? names) 
	{
		string[] result = [];
		if (names != null) 
		{
			foreach (string name in names) 
			{
				string? parsed = await Parse(name, Utils.CreateHash(names));
				if (parsed != null)
					result = result.Append(parsed).ToArray();
			}
			return result;
		}
		return null;
	}
	
	public async Task<string?> Parse(string gameName, string identifier) 
	{
		string formatted = gameName.Trim();
		DBOperation<GameIdPair> exists = new() 
		{
			identifier = identifier,
			opType = OperationType.Contains,
			data = new GameIdPair() { name = formatted }
		};
		bool result = (bool)(await context.DoOperation(exists)).result;
		if (result) 
		{
			exists.opType = OperationType.Retrieve;
			return (string)(await context.DoOperation(exists)).result;
		}
		string? parsed = await ParseGameName(formatted);
		if (parsed != null) 
		{
			DBOperation<GameIdPair> insert = new() 
			{
				identifier = identifier,
				opType = OperationType.Insert,
				data = new GameIdPair() 
				{
					name = formatted,
					id = parsed
				}
			};
			await context.DoOperation(insert);
			return parsed;
		}
		return null;
	}
	
	private bool isId(string check) => ulong.TryParse(check, out ulong _);
	
	private async Task<string?> ParseGameName(string name) 
	{
		using HttpClient client = new();
		client.DefaultRequestHeaders.Add("Client-ID", config.client_id);
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.oauth_token}");

		string url = $"https://api.twitch.tv/helix/games?name={name}";
		
		HttpResponseMessage response = await client.GetAsync(url);
		
		string content = await response.Content.ReadAsStringAsync();
		
		if (response.IsSuccessStatusCode) 
		{
			JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
			JArray games = (JArray)json.GetValue("data");
			string id = (string)((JObject)games.First).GetValue("id");
			if (id != null)
				return id;
		}
		return null;
	}
}