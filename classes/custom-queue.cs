using GrassGuy.Structs;

namespace GrassGuy;

public struct DequeueItem
{
	public StreamGrabRequest item;
	public string hash;
}

public class UniqueQueue
{
	private Queue<StreamGrabRequest> _queue;
	private readonly HashSet<StreamGrabRequest> _set;
	private readonly Dictionary<StreamGrabRequest, string> _hashes;

	public UniqueQueue()
	{
		_queue = new Queue<StreamGrabRequest>();
		_set = [];
		_hashes = [];
	}

	public string Enqueue(StreamGrabRequest item)
	{
		if (_set.Add(item))
		{
			string hash = Utils.CreateHash(item);
			_queue.Enqueue(item);
			_hashes[item] = hash;
			return hash;
		}
		return _hashes[item];
	}

	public DequeueItem Dequeue()
	{
		if (_queue.Count == 0)
		{
			throw new InvalidOperationException("Queue is empty.");
		}
		StreamGrabRequest item = _queue.Dequeue();
		_set.Remove(item);
		DequeueItem dequeued = new()
		{
			item = item,
			hash = _hashes[item]
		};
		_hashes.Remove(item);
		return dequeued;
	}

	public StreamGrabRequest Peek()
	{
		if (_queue.Count == 0)
		{
			throw new InvalidOperationException("Queue is empty.");
		}
		return _queue.Peek();
	}

	public bool Contains(StreamGrabRequest item)
	{
		return _set.Contains(item);
	}
	
	public bool Contains(Func<StreamGrabRequest, bool> func) 
	{
		return _set.Any(func);
	}
	
	public string? GetHash(StreamGrabRequest item)
	{
		return _hashes.TryGetValue(item, out string hash) ? (string?)hash : null;
	}

	public int Count => _queue.Count;

	public void Clear()
	{
		_queue.Clear();
		_set.Clear();
	}
}
