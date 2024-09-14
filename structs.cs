namespace GrassGuy.Structs;

public struct Stream 
{
	public string id;
	public string user_id;
	public string user_login;
	public string user_name;
	public string game_id;
	public string game_name;
	public string type;
	public string title;
	public string[] tags;
	public int viewer_count;
	public string started_at;
	public string language;
	public string thumbnail_url;
	public bool is_mature;
}

public struct StreamGrabRequest 
{
	public ulong guild_id;
	public string[]? user_ids;
	public string[]? user_logins;
	public string[]? game_ids;
	public int? first;
	public string? before;
	public string? after;
}

public struct PaginationInfo 
{
	public string? cursor;
}

public struct StreamHandlerConfig
{
	public string client_id;
	public string oauth_token;
	public string client_secret;
	public string refresh_token;
}

public struct StreamResponse 
{
	public Stream[] data;
	public PaginationInfo pagination;
}

public struct ValidTokenResponse 
{
	public string client_id;
	public string login;
	public string[] scopes;
	public string user_id;
	public long expires_in;
}

public struct InvalidTokenResponse 
{
	public uint status;
	public string message;
}