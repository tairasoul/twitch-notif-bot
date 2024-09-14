using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Discord;
using LinqToDB;
using LinqToDB.Concurrency;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;

namespace GrassGuy;

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
		set => _isSendingStreams = (value ? "true" : "false");
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
	
	public ITable<GuildConfig> GuildConfigs => this.GetTable<GuildConfig>();
	public ITable<GameIdPair> GameIdPairs => this.GetTable<GameIdPair>();
}

public enum OperationType 
{
	Insert = 0,
	Retrieve = 1,
	Contains = 2,
	Delete = 3,
	Update = 4
}

public struct DBOperation<T> 
{
	public string identifier;
	public OperationType opType;
	public T? data;
	public object result;
}

public class ContextWrapper(ConfigDBContext context) {
	private ConfigDBContext db = context;
	private Queue<DBOperation<GuildConfig>> configActive = new();
	private Queue<DBOperation<GameIdPair>> idCacheActive = new();
	
	public event Action<DBOperation<GuildConfig>> configExecuted = (DBOperation<GuildConfig> _) => 
	{
		
	};
	public event Action<DBOperation<GameIdPair>> idExecuted = (DBOperation<GameIdPair> _) => 
	{
		
	};
	private bool handlingId = false;
	private bool handlingConfig = false;
	
	public void StartHandlingOperations() 
	{
		_ = Task.Run(handleConfig);
		_ = Task.Run(handleId);
	}
	
	private async void handleId() 
	{
		await Task.Delay(10);
		if (!handlingId)
		if (idCacheActive.TryDequeue(out DBOperation<GameIdPair> config)) 
		{
			if ((DBOperation<GameIdPair>?)config != null) 
			{
				handlingId = true;
				try 
				{
					switch (config.opType) 
					{
						case OperationType.Insert:
							db.Insert(config.data);
							break;
						case OperationType.Retrieve:
							config.result = db.GameIdPairs.First((v) => v.name == config.data.name);
							break;
						case OperationType.Contains:
							config.result = db.GameIdPairs.Any() && db.GameIdPairs.Contains(new GameIdPair(){name = config.data.name});
							break;
						case OperationType.Delete:
							db.Delete(config.data);
							break;
						case OperationType.Update:
							db.Update(config.data);
							break;
					}
				}
				catch (ArgumentOutOfRangeException _) 
				{
					
				}
				catch (System.InvalidOperationException _) 
				{
					
				}
				idExecuted.Invoke(config);
				handlingId = false;
			}
		}
		_ = Task.Run(handleId);
	}
	
	private async void handleConfig() 
	{
		await Task.Delay(20);
		if (!handlingConfig)
		if (configActive.TryDequeue(out DBOperation<GuildConfig> config)) 
		{
			if ((DBOperation<GuildConfig>?)config != null)
			{
				handlingConfig = true;
				try {
					switch (config.opType) 
					{
						case OperationType.Insert:
							db.Insert(config.data);
							break;
						case OperationType.Retrieve:
							config.result = db.GuildConfigs.First((g) => g.guild_id == config.data.guild_id);
							break;
						case OperationType.Contains:
							config.result = db.GuildConfigs.Any() && db.GuildConfigs.Contains(new GuildConfig() {guild_id = config.data.guild_id});
							break;
						case OperationType.Delete:
							db.Delete(config.data);
							break;
						case OperationType.Update:
							db.Update(config.data);
							break;
					}
				}
				catch (ArgumentOutOfRangeException _) 
				{
					
				}
				catch (System.InvalidOperationException _) 
				{
					
				}
				configExecuted.Invoke(config);
				handlingConfig = false;
			}
		}
		_ = Task.Run(handleConfig);
	}
	
	public async Task<DBOperation<GuildConfig>> DoOperation(DBOperation<GuildConfig> operation) 
	{
		if (configActive.Any()) 
		{
			if (configActive.Any((v) => v.identifier == operation.identifier)) 
			{
				DBOperation<GuildConfig> config = configActive.First((v) => v.identifier == operation.identifier);
				if (config.opType == operation.opType) 
				{
					DBOperation<GuildConfig>? result = null;
					void listener(DBOperation<GuildConfig> op)
					{
						if (op.identifier == config.identifier)
						{
							result = op;
							configExecuted -= listener;
						}
					}
					configExecuted += listener;
					while (true) 
					{
						if (result != null) 
							break;
						await Task.Delay(5);
					}
					return (DBOperation<GuildConfig>)result;
				}
			}
		}
		configActive.Enqueue(operation);
		DBOperation<GuildConfig>? res = null;
		void listen_(DBOperation<GuildConfig> op)
		{
			if (op.identifier == operation.identifier)
			{
				res = op;
				configExecuted -= listen_;
			}
		}
		configExecuted += listen_;
		while (true) 
		{
			if (res != null) 
				break;
			await Task.Delay(5);
		}
		return (DBOperation<GuildConfig>)res;
	}
	
	
	public async Task<DBOperation<GameIdPair>> DoOperation(DBOperation<GameIdPair> operation) 
	{
		if (idCacheActive.Any()) 
		{
			if (idCacheActive.Any((v) => v.identifier == operation.identifier)) 
			{
				DBOperation<GameIdPair> config = idCacheActive.First((v) => v.identifier == operation.identifier);
				if (config.opType == operation.opType) 
				{
					DBOperation<GameIdPair>? result = null;
					void listener(DBOperation<GameIdPair> op)
					{
						if (op.identifier == config.identifier)
						{
							result = op;
							idExecuted -= listener;
						}
					}
					idExecuted += listener;
					while (true) 
					{
						if (result != null) 
							break;
						await Task.Delay(5);
					}
					return (DBOperation<GameIdPair>)result;
				}
			}
		}
		idCacheActive.Enqueue(operation);
		DBOperation<GameIdPair>? res = null;
		void listen_(DBOperation<GameIdPair> op)
		{
			if (op.identifier == operation.identifier)
			{
				res = op;
				idExecuted -= listen_;
			}
		}
		idExecuted += listen_;
		while (true) 
		{
			if (res != null) 
				break;
			await Task.Delay(5);
		}
		return (DBOperation<GameIdPair>)res;
	}
}

public class GuildConfigHandler(string databasePath)
{
	private ConfigDBContext db;
	internal ContextWrapper wrapper;
	public void SetupDatabase() 
	{
		db = new(databasePath);
		db.EnsureDatabaseSetup();
		wrapper = new(db);
		wrapper.StartHandlingOperations();
	}
	
	public async void GuildAdded(ulong guildId) 
	{
		DBOperation<GuildConfig> operation = new() 
		{
			identifier = guildId.ToString(),
			opType = OperationType.Insert,
			data = new GuildConfig() 
			{
				guild_id = guildId,
				isSendingStreams = false
			}
		};
		await wrapper.DoOperation(operation);
	}
	
	public async void GuildRemoved(ulong guildId) 
	{
		DBOperation<GuildConfig> operation = new() 
		{
			identifier = guildId.ToString(),
			opType = OperationType.Delete,
			data = new GuildConfig() 
			{
				guild_id = guildId
			}
		};
		await wrapper.DoOperation(operation);
	}
	
	public async Task<GuildConfig> RetrieveConfig(ulong guildId) 
	{
		DBOperation<GuildConfig> config = new() 
		{
			identifier = guildId.ToString(),
			opType = OperationType.Retrieve,
			data = new GuildConfig() 
			{
				guild_id = guildId
			}
		};
		var result = await wrapper.DoOperation(config);
		Console.WriteLine(result.data);
		Console.WriteLine(result.identifier);
		Console.WriteLine(result.opType);
		Console.WriteLine(result.result);
		if (result.result == null) 
		{
			await Task.Delay(100);
			result = await wrapper.DoOperation(config);
		}
		return (GuildConfig)result.result;
	}
	
	public async Task<bool> GuildHasConfig(ulong guildId) {
		DBOperation<GuildConfig> config = new() 
		{
			identifier = guildId.ToString(),
			opType = OperationType.Contains,
			data = new GuildConfig() 
			{
				guild_id = guildId
			}
		};
		return (bool)(await wrapper.DoOperation(config)).result;
	}
	
	public async void UpdateConfig(GuildConfig config) 
	{
		DBOperation<GuildConfig> cfg = new() 
		{
			identifier = config.guild_id.ToString(),
			opType = OperationType.Update,
			data = config
		};
		await wrapper.DoOperation(cfg);
	}
}