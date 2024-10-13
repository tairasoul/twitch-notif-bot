using Discord;
using Discord.WebSocket;
using GrassGuy.Structs;
using Newtonsoft.Json.Linq;

namespace GrassGuy;

public class StreamSender(GuildConfigHandler confs, DiscordSocketClient clien, StreamHandler stream, GameNameIdCache cache)
{
	public GuildConfigHandler configs = confs;
	public DiscordSocketClient client = clien;
	public StreamHandler streamHandler = stream;
	
	public GameNameIdCache cache = cache;
	
	private string[] sent_streams = [];
	
	public async void DoGuildChecks() 
	{
		try 
		{
			foreach (SocketGuild guild in client.Guilds) 
			{
				if (!configs.GuildHasConfig(guild.Id))
					continue;
				GuildConfig config = configs.RetrieveConfig(guild.Id);
				if (config.isSendingStreams && config.stream_channel != null) 
				{
					if (streamHandler.HasRequestForGuild(guild.Id)) 
						continue;
					StreamGrabRequest request = new() 
					{
						first = 100,
						guild_id = guild.Id,
						user_ids = config.user_ids,
						game_ids = await cache.ParseGameNames(config.game_names),
						user_logins = config.user_logins
					};
					string listeningId = streamHandler.AddRequest(request);
					Action<string, StreamResponse>? listener; 
					listener = (string hash, StreamResponse response) => 
					{
						if (hash == listeningId) 
						{
							foreach (Structs.Stream stream in response.data) 
							{
								if (!sent_streams.Contains(stream.id)) 
								{
									sent_streams = sent_streams.Append(stream.id).ToArray();
									Task.Run(() => SendMessage(config, stream, guild));
								}
							}
						}
					};
					streamHandler.StreamsGrabbed += listener;
				}
			}
		}
		catch (System.InvalidCastException _) 
		{
			
		}
		catch (System.NullReferenceException _) 
		{
			
		}
		await Task.Delay(2000);
		_ = Task.Run(DoGuildChecks);
	}
	
	private async Task SendMessage(GuildConfig config, Structs.Stream stream, SocketGuild guild) 
	{
		SocketTextChannel channel = (SocketTextChannel)guild.GetChannel((ulong)config.stream_channel);
		Embed StreamEmbed = await CreateEmbedForStream(stream);
		await channel.SendMessageAsync(text: config.role_id != null ? $"<@&{config.role_id}>" : null,embeds: [StreamEmbed]);
	}
	
	private async Task<UserGrab> GetUser(string id) 
	{
		using HttpClient client = new();
		client.DefaultRequestHeaders.Add("Client-ID", streamHandler.config.client_id);
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {streamHandler.config.oauth_token}");

		string url = $"https://api.twitch.tv/helix/users?id={id}";
		HttpResponseMessage response = await client.GetAsync(url);
		if (!response.IsSuccessStatusCode) 
		{
			Console.WriteLine("Error in GetUser");
			string responseData = await response.Content.ReadAsStringAsync();
			JObject responseJson = JObject.Parse(responseData);
			Console.WriteLine("Response json deconstructed:");
			foreach (KeyValuePair<string, JToken?> pair in responseJson)
			{
				Console.WriteLine($"{pair.Key}: {pair.Value}");
			}
			return new UserGrab() 
			{
				profile_image_url = "",
				display_name = ""
			};
		}
		else 
		{
			string content = await response.Content.ReadAsStringAsync();
			JObject jsonObject = JObject.Parse(content);
			JArray? data = (JArray?)jsonObject.GetValue("data");
			UserGrab grab = new();
			UnparsedUserGrab unparsed = GetUnparsed(data);
			grab.created_at = unparsed.created_at;
			grab.description = unparsed.description;
			grab.id = unparsed.id;
			grab.login = unparsed.login;
			grab.display_name = unparsed.display_name;
			grab.offline_image_url = unparsed.offline_image_url;
			grab.profile_image_url = unparsed.profile_image_url;
			grab.broadcasterType = GetBroadcasterType(unparsed.broadcasterType);
			grab.type = GetType(unparsed.type);
			return grab;
		}
	}
	
	private UnparsedUserGrab GetUnparsed(JArray data) 
	{
		JObject d = data.FirstOrDefault() as JObject;
		UnparsedUserGrab grab = new()
		{
			created_at = d.GetValue("created_at").ToString(),
			description = d.GetValue("description").ToString(),
			id = d.GetValue("id").ToString(),
			login = d.GetValue("login").ToString(),
			offline_image_url = d.GetValue("offline_image_url").ToString(),
			profile_image_url = d.GetValue("profile_image_url").ToString(),
			type = d.GetValue("type").ToString(),
			broadcasterType = d.GetValue("broadcaster_type").ToString()
		};
		return grab;
	}
	
	private UserType GetType(string type) 
	{
		return type switch
		{
			"admin" => UserType.Admin,
			"global_mod" => UserType.Global_Mod,
			"staff" => UserType.Staff,
			_ => UserType.RegularUser,
		};
	}
	
	private BroadcasterType GetBroadcasterType(string type) 
	{
		return type switch 
		{
			"affiliate" => BroadcasterType.Affiliate,
			"partner" => BroadcasterType.Partner,
			_ => BroadcasterType.Regular
		};
	}
	
	private async Task<Embed> CreateEmbedForStream(Structs.Stream stream) 
	{
		UserGrab grab = await GetUser(stream.user_id);
		EmbedBuilder builder = new()
		{
			Title = stream.title,
			Description = $"{stream.user_name ?? stream.user_login} is streaming {stream.game_name} with {stream.viewer_count} viewers.\n[Watch here!](https://twitch.tv/{stream.user_login})",
			Color = Color.Purple,
			ImageUrl = stream.thumbnail_url.Replace("{width}", "1920").Replace("{height}", "1080"),
			Footer = new EmbedFooterBuilder
			{
				Text = "Made with Twitch API and Discord.NET"
			},
			Author = new EmbedAuthorBuilder 
			{
				Name = grab.display_name ?? grab.login,
				IconUrl = grab.profile_image_url
			},
			ThumbnailUrl = $"https://static-cdn.jtvnw.net/ttv-boxart/{stream.game_id}_IGDB-90x120.jpg"
		};
		builder.AddField("Mature stream", stream.is_mature, true);
		builder.AddField("Language", stream.language, true);
		DateTimeOffset startTime = DateTime.Parse(stream.started_at);
		builder.AddField("Started", $"<t:{startTime.ToUnixTimeSeconds()}:R>", true);
		return builder.Build();
	}
}