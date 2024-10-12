using System.Text.Json;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;

namespace GrassGuy;

public class DatabaseInfo 
{
	public GuildConfig[] configs;
	public GameIdPair[] pairs;
	public GuildConfig GetConfig(ulong guildId) 
	{
		return configs.FirstOrDefault((v) => v.guild_id == guildId);
	}
	public GameIdPair GetPair(string arg, bool isId = false) 
	{
		if (isId)
			return pairs.FirstOrDefault((v) => v.id == arg);
		else
			return pairs.FirstOrDefault((v) => v.name == arg);
	}
	public bool HasConfig(ulong guildId) 
	{
		return configs.Any((v) => v.guild_id == guildId);
	} 
	
	public bool HasPair(string arg, bool isId = false) 
	{
		if (isId)
			return pairs.Any((v) => v.id == arg);
		else
			return pairs.Any((v) => v.name == arg);
	}
	
	public void AddPair(GameIdPair pair) 
	{
		pairs = [..pairs, pair];
	}
	
	public void RemovePair(string id) 
	{
		pairs = pairs.Where((v) => v.id != id).ToArray();
	}
	
	private void AddConfig(GuildConfig cfg) 
	{
		configs = [..configs, cfg];
	}
	
	private void UpdateConfig(GuildConfig cfg) 
	{
		foreach (GuildConfig config in configs) 
		{
			if (config.guild_id == cfg.guild_id) 
			{
				config.game_names = cfg.game_names;
				config.isSendingStreams = cfg.isSendingStreams;
				config.role_id = cfg.role_id;
				config.stream_channel = cfg.stream_channel;
				config.user_ids = cfg.user_ids;
				config.user_logins = cfg.user_logins;
				break;
			}
		}
	}
	
	public void RemoveConfig(ulong guildId) 
	{
		configs = configs.Where((v) => v.guild_id != guildId).ToArray();
	}
	
	public void AddOrUpdateConfig(GuildConfig cfg) 
	{
		if (HasConfig(cfg.guild_id))
			UpdateConfig(cfg);
		else
			AddConfig(cfg);
	}
}

[Table(Name = "GuildConfigs")]
public class GuildConfig 
{
	[PrimaryKey, NotNull] public ulong guild_id { get; set; }
	[Column, Nullable] public ulong? stream_channel { get; set; }
	[Column, Nullable] public ulong? role_id { get; set; }
	[Column, NotNull] public string _isSendingStreams { get; set; }
	[Column, Nullable] public string game_names_json { get; set; }
	[Column, Nullable] public string user_logins_json { get; set; }
	[Column, Nullable] public string user_ids_json { get; set; }
	
	[NotColumn]
	public bool isSendingStreams
	
	{
		get => _isSendingStreams == "true";
		set => _isSendingStreams = value ? "true" : "false";
	}
	
	[NotColumn]
	public string[]? game_names
	{
		get => string.IsNullOrEmpty(game_names_json) ? null : JsonSerializer.Deserialize<string[]>(game_names_json);
		set => game_names_json = JsonSerializer.Serialize(value);
	}
	
	[NotColumn]
	public string[]? user_logins
	
	{
		get => string.IsNullOrEmpty(user_logins_json) ? null : JsonSerializer.Deserialize<string[]>(user_logins_json);
		set => user_logins_json = JsonSerializer.Serialize(value);
	}
	
	[NotColumn]
	public string[]? user_ids
	
	{
		get => string.IsNullOrEmpty(user_ids_json) ? null : JsonSerializer.Deserialize<string[]>(user_ids_json);
		set => user_ids_json = JsonSerializer.Serialize(value);
	}
}

[Table(Name = "GameIdPairCache")]
public class GameIdPair 
{
	[PrimaryKey, NotNull]
	public string name { get; set; }
	[Column, NotNull]
	public string id { get; set; }
}

public class ConfigDBContext(string databasePath) : DataConnection(ProviderName.SQLite, $"Data Source={databasePath}") 
{
	public void EnsureDatabaseSetup() 
	{
		this.CreateTable<GuildConfig>(tableOptions: TableOptions.CreateIfNotExists);
		this.CreateTable<GameIdPair>(tableOptions: TableOptions.CreateIfNotExists);
	}
	
	public void DumpInfo(DatabaseInfo info) 
	{
		foreach (GuildConfig cfg in info.configs) 
		{
			if (!GuildConfigs.Any((v) => v.guild_id == cfg.guild_id))
				this.Insert(cfg);
			else
				this.Update(cfg);
		}
		foreach (GameIdPair pair in info.pairs) 
		{
			if (!GameIdPairs.Any((v) => v.id == pair.id))
				this.Insert(pair);
			else
				this.Update(pair);
		}
	}
	
	public DatabaseInfo LoadInfo() 
	{
		return new()
		{
			configs = [.. GuildConfigs],
			pairs = [.. GameIdPairs]
		};
	}
	
	public ITable<GuildConfig> GuildConfigs => this.GetTable<GuildConfig>();
	public ITable<GameIdPair> GameIdPairs => this.GetTable<GameIdPair>();
}

public class GuildConfigHandler(string databasePath)
{
	internal DatabaseInfo info;
	private ConfigDBContext db;
	
	private async Task DatabaseDumpLoop() 
	{
		while (true) 
		{
			await Task.Delay(5000);
			db.DumpInfo(info);
		}
	}
	
	public void SetupDatabase() 
	{
		db = new(databasePath);
		db.EnsureDatabaseSetup();
		info = db.LoadInfo();
		Task.Run(DatabaseDumpLoop);
	}
	
	public void GuildAdded(ulong guildId) 
	{
		GuildConfig data = new() 
		{
			guild_id = guildId,
			isSendingStreams = false
		};
		
		info.AddOrUpdateConfig(data);
	}
	
	public void GuildRemoved(ulong guildId) 
	{
		info.RemoveConfig(guildId);
	}
	
	public GuildConfig RetrieveConfig(ulong guildId) 
	{
		return info.GetConfig(guildId);
	}
	
	public bool GuildHasConfig(ulong guildId) 
	{
		return info.HasConfig(guildId);
	}
	
	public void UpdateConfig(GuildConfig config) 
	{
		info.AddOrUpdateConfig(config);
	}
}