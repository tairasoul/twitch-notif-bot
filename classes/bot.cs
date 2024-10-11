using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using GrassGuy.Structs;
using System.Runtime.ConstrainedExecution;

namespace GrassGuy;

public class Bot 
{
	private IServiceProvider _services;
	private readonly string _token;
	private readonly string _twitch_token;
	private readonly string _twitch_secret;
	private readonly string _twitch_id;
	private readonly string _twitch_refresh_token;
	private readonly string basePath;
	private readonly string db_path;

	private readonly DiscordSocketConfig _socketConfig = new()
	{
		GatewayIntents = GatewayIntents.All,
		AlwaysDownloadUsers = true,
		MaxWaitBetweenGuildAvailablesBeforeReady = 1000
	};
	
	private readonly StreamHandlerConfig config;
	public Bot(string basePath) 
	{
		this.basePath = basePath;
		string data = File.ReadAllText(Path.Combine(basePath, "config.json"));
		string twitchCredentials = File.ReadAllText(Path.Combine(basePath, "twitch.credentials.json"));
		JObject obj = JObject.Parse(data);
		_token = obj.GetValue("token").ToString();
		db_path = obj.GetValue("database_path").ToString();
		JObject twitch = JObject.Parse(twitchCredentials);
		_twitch_token = twitch.GetValue("oauth_token").ToString();
		_twitch_secret = twitch.GetValue("client_secret").ToString();
		_twitch_id = twitch.GetValue("client_id").ToString();
		_twitch_refresh_token = twitch.GetValue("refresh_token").ToString();
		config = new() 
		{
			oauth_token = _twitch_token,
			client_secret = _twitch_secret,
			client_id = _twitch_id,
			refresh_token = _twitch_refresh_token
		};
	}

	public async Task Main()
	{	
		_services = new ServiceCollection()
			.AddSingleton(_socketConfig)
			.AddSingleton<DiscordSocketClient>()
			.AddSingleton(x => new GuildConfigHandler(Path.Combine(basePath, db_path)))
			.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
			.AddSingleton<InteractionHandler>()
			.AddSingleton(x => new StreamHandler(config, Path.Combine(basePath, "twitch.credentials.json")))
			.AddSingleton(x => new GameNameIdCache(_services.GetRequiredService<GuildConfigHandler>(), config))
			.AddSingleton(x => new StreamSender(
				_services.GetRequiredService<GuildConfigHandler>(),
				_services.GetRequiredService<DiscordSocketClient>(),
				_services.GetRequiredService<StreamHandler>(),
				_services.GetRequiredService<GameNameIdCache>()
			))
			.BuildServiceProvider();

		var client = _services.GetRequiredService<DiscordSocketClient>();

		client.Log += LogAsync;
		
		_services.GetRequiredService<GuildConfigHandler>().SetupDatabase();

		// Here we can initialize the service that will register and execute our commands
		await _services.GetRequiredService<InteractionHandler>()
			.InitializeAsync();
			
		StreamHandler handler = _services.GetRequiredService<StreamHandler>();
		
		Task TwitchRefreshTask = Task.Run(handler.TwitchRefresh);
		Task StreamHandlerTask = Task.Run(handler.StartHandling);
		
		// Bot token can be provided from the Configuration object we set up earlier
		await client.LoginAsync(TokenType.Bot, _token);
		await client.StartAsync();
		
		client.GuildAvailable += GuildAvailable;
		client.JoinedGuild += GuildAvailable;
		client.LeftGuild += GuildRemoved;

		// Never quit the program until manually forced to.
		await Task.Delay(Timeout.Infinite);
	}
	
	private async Task GuildAvailable(SocketGuild guild) 
	{
		GuildConfigHandler configHandler =  _services.GetRequiredService<GuildConfigHandler>();
		configHandler.GuildAdded(guild.Id);
	}
	
	private async Task GuildRemoved(SocketGuild guild) 
	{
		GuildConfigHandler configHandler =  _services.GetRequiredService<GuildConfigHandler>();
		configHandler.GuildRemoved(guild.Id);
	}
	
	private async Task EnsureGuildsExist() 
	{
		DiscordSocketClient client = _services.GetRequiredService<DiscordSocketClient>();
		GuildConfigHandler configHandler =  _services.GetRequiredService<GuildConfigHandler>();
		foreach (SocketGuild guild in client.Guilds) 
		{
			if (!configHandler.GuildHasConfig(guild.Id)) 
			{
				configHandler.GuildAdded(guild.Id);
			}
		}
		_ = Task.Run(_services.GetRequiredService<StreamSender>().DoGuildChecks);
	}

	private Task LogAsync(LogMessage message)
	{
		Console.WriteLine(message.ToString());
		return Task.CompletedTask;
	}
}