using Discord;
using Discord.WebSocket;
using GrassGuy.Structs;

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
				if (!await configs.GuildHasConfig(guild.Id))
					continue;
				GuildConfig config = await configs.RetrieveConfig(guild.Id);
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
		Embed StreamEmbed = CreateEmbedForStream(stream);
		await channel.SendMessageAsync(text: $"<@&{config.role_id}>",embeds: [StreamEmbed]);
	}
	
	private Embed CreateEmbedForStream(Structs.Stream stream) 
	{
		EmbedBuilder builder = new()
		{
			Title = stream.title,
			Description = $"{stream.user_name ?? stream.user_login} is streaming {stream.game_name} with {stream.viewer_count} viewers.\n[Watch](https://twitch.tv/{stream.user_login})",
			Color = Color.Purple,
			ImageUrl = stream.thumbnail_url.Replace("{width}", "1920").Replace("{height}", "1080"),
			Footer = new EmbedFooterBuilder
			{
				Text = "Made with Twitch API and Discord.NET"
			}
		};
		builder.AddField("isMature", stream.is_mature, true);
		builder.AddField("Language", stream.language, true);
		DateTimeOffset startTime = DateTime.Parse(stream.started_at);
		builder.AddField("Started at", $"<t:{startTime.ToUnixTimeSeconds()}>", true);
		return builder.Build();
	}
}