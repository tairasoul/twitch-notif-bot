namespace GrassGuy;

public class SentStreams 
{
	internal static string[] sent_streams = [];
	
	public bool StreamIsSent(string id) 
	{
		return sent_streams.Contains(id);
	}
	
	public void StreamSent(string id) 
	{
		sent_streams = sent_streams.Append(id).ToArray();
	}
}