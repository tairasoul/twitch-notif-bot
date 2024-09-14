using System.Security.Cryptography;
using System.Text;
using GrassGuy.Structs;

namespace GrassGuy;

public class Utils 
{
	public static string Join(char separator, string[] items) 
	{
		string output = "";
		foreach (string item in items) 
		{
			output += item;
			output += separator;
		}
		output = output.Trim(separator);
		return output;
	}
	
	public static string CreateHash(string[] items) 
	{
		using MD5 md5 = MD5.Create();
		string inputString = Join(';', items);
		byte[] inputBytes = Encoding.UTF8.GetBytes(inputString);
		byte[] hashBytes = md5.ComputeHash(inputBytes);
		return hashBytes.ToString();
	}
	
	public static string CreateHash(StreamGrabRequest item) 
	{
		using MD5 md5 = MD5.Create();
		string inputString = "";
		if (item.after != null)
			inputString += $"after:{item.after}";
		if (item.before != null)
			inputString += $"before:{item.before}";
		if (item.first != null)
			inputString += $"first:{item.first}";
		if (item.game_ids != null)
			inputString += $"game_ids:{Join(';', item.game_ids)}";
		if (item.user_ids != null)
			inputString += $"user_ids:{Join(';', item.user_ids)}";
		if (item.user_logins != null)
			inputString += $"user_logins:{Join(';', item.user_logins)}";
		byte[] inputBytes = Encoding.UTF8.GetBytes(inputString);
		byte[] hashBytes = md5.ComputeHash(inputBytes);
		return hashBytes.ToString();
	}
	
	public static string? GetHeaderValue(HttpResponseMessage response, string header) 
	{
		if (response.Headers.TryGetValues(header, out IEnumerable<string>? values)) 
		{
			return string.Join(", ", values);
		}
		return null;
	}
}