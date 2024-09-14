using Discord;
using Discord.Interactions;

namespace GrassGuy.Modules;

public class EmbedUtils 
{
	public static Embed CreateEmbedWithDescription(string description) 
	{
		EmbedBuilder builder = new()
		{
			Description = description,
			Color = Color.Purple,
			Footer = new EmbedFooterBuilder
			{
				Text = "Made with Twitch API and Discord.NET"
			}
		};
		return builder.Build();
	}
}

public class MainCommandModule : InteractionModuleBase<SocketInteractionContext>
{
	public InteractionService Commands { get; set; }

	[Group("stream", "Stream options.")]
	public class StreamClass : InteractionModuleBase<SocketInteractionContext>
	{
		public GuildConfigHandler guildConfig { get; set; }
		[SlashCommand("settings", "View current settings.")]
		public async Task ViewSettings() 
		{
			GuildConfig config = await guildConfig.RetrieveConfig(Context.Guild.Id);
			EmbedBuilder builder = new()
			{
				Title = $"Settings for {Context.Guild.Name}",
				Color = Color.Purple,
				Footer = new EmbedFooterBuilder
				{
					Text = "Made with Twitch API and Discord.NET"
				}
			};
			builder.AddField("Game Names", config.game_names != null ? string.Join(", ", config.game_names) : "None.", true);
			builder.AddField("User Ids", config.user_ids != null ? string.Join(", ", config.user_ids) : "None.", true);
			builder.AddField("User Logins", config.user_logins != null ? string.Join(", ", config.user_logins) : "None.", true);
			builder.AddField("Role Pinged", config.role_id != null ? $"<@&{config.role_id}>" : "None.", true);
			builder.AddField("Stream Channel", config.stream_channel != null ? $"<#{config.stream_channel}>" : "None.", true);
			builder.AddField("Active", config.isSendingStreams ? "Active." : "Inactive.", true);
			await RespondAsync(embed: builder.Build());
		}
		[RequireUserPermission(GuildPermission.Administrator)]
		[Group("set", "Setter commands for stream options.")]
		public class SetStream : InteractionModuleBase<SocketInteractionContext> 
		{
			public GuildConfigHandler guildConfig { get; set; }
			public GameNameIdCache cache { get; set; }
			[SlashCommand("channel", "Set the stream channel.")]
			public async Task SetStreamChannel([Summary(description: "Channel to send streams in.")] ITextChannel channel) 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.stream_channel = channel.Id;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription($"Set stream channel to <#{channel.Id}>."));
			}
			[SlashCommand("status", "Set status.")]
			public async Task Start([Summary(description: "Do we send streams?")] bool active) 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.isSendingStreams = active;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription(active ? $"Now sending streams{(currentConfig.stream_channel != null ? $" in <#{currentConfig.stream_channel}>" : "")}." : "No longer sending streams."));
			}
			[SlashCommand("game-names", "Set game names.")]
			public async Task SetGameIds([Summary(description: "Game names to watch. Comma separated. Maximum of 100.")] string game_names) 
			{
				await DeferAsync();
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.game_names = game_names.Split(',');
				try 
				{
					await cache.ParseGameNames(currentConfig.game_names);
				}
				catch (System.NullReferenceException _)
				{
					
				}
				guildConfig.UpdateConfig(currentConfig);
				await ModifyOriginalResponseAsync((MessageProperties props) => props.Embed = EmbedUtils.CreateEmbedWithDescription($"Now watching for new streams on game names `{string.Join(", ", game_names)}`."));
			}
			[SlashCommand("user-logins", "Set user logins (username in URL).")]
			public async Task SetUserLogins([Summary(description: "User logins to watch. Comma separated. Maximum of 100.")] string user_logins) 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.user_logins = user_logins.Split(',');
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription($"Now watching for new streams on user ids `{string.Join(", ", user_logins)}`."));
			}
			[SlashCommand("user-ids", "Set user ids.")]
			public async Task SetUserIds([Summary(description: "User ids to watch. Comma separated. Maximum of 100.")] string user_ids) 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.user_ids = user_ids.Split(',');
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription($"Now watching for new streams on user ids `{string.Join(", ", user_ids)}`."));
			}
			[SlashCommand("role", "Set role to ping.")]
			public async Task SetRoleId([Summary(description: "Role to ping when new streams are found.")] IRole role) 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.role_id = role.Id;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription($"Now pinging role <@&{role.Id}>"));
			}
		}
		[RequireUserPermission(GuildPermission.Administrator)]
		[Group("unset", "Unsetter commands for stream options.")]
		public class UnsetStream : InteractionModuleBase<SocketInteractionContext> 
		{
			
			public GuildConfigHandler guildConfig { get; set; }
			[SlashCommand("channel", "Unset the stream channel.")]
			public async Task SetStreamChannel() 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.stream_channel = null;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription($"Unset stream channel."));
			}
			[SlashCommand("game-names", "Unset game names.")]
			public async Task SetGameIds() 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.game_names = null;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription("Unset game ids."));
			}
			[SlashCommand("user-logins", "Unset user logins (username in URL).")]
			public async Task SetUserLogins() 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.user_logins = null;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription("Unset user logins."));
			}
			[SlashCommand("user-ids", "Unset user ids.")]
			public async Task SetUserIds() 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.user_ids = null;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription("Unset user ids."));
			}
			[SlashCommand("role", "Unset role to ping.")]
			public async Task SetRoleId() 
			{
				GuildConfig currentConfig = await guildConfig.RetrieveConfig(Context.Guild.Id);
				currentConfig.role_id = null;
				guildConfig.UpdateConfig(currentConfig);
				await RespondAsync(embed: EmbedUtils.CreateEmbedWithDescription("Unset role id."));
			}
		}
	}
}