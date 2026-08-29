namespace DesktopPet.Core.Skins;

/// <summary>
/// 以「格數」計量的 LRU 解碼快取（設計檔 §7.3.6 / §10.1）：延遲載入，首次抽中才解碼，
/// 之後保留於快取；容量以<b>總格數</b>而非單元數計，上限預設 48 格／每隻寵物。
/// </summary>
/// <remarks>
/// <b>為何以格數計容量：</b>一個 8 格 256×256 單元解碼後約 2 MB，若以「單元數」計上限，
/// 使用者全用 16 格動畫時會撞破 §10.1 的記憶體上限；改以「總計 48 格」計即可封頂（§7.3.6）。
/// <para>
/// 泛型化 <typeparamref name="TValue"/>（正式為 WPF <c>BitmapSource</c>）讓本快取<b>不依賴 WPF</b>，
/// 可跨平台單元測試 LRU 淘汰與「僅解碼一次」的行為（以 <see cref="DecodeCount"/> 觀測）。
/// 解碼透過傳入的工廠委派完成，快取本身不知如何解碼。
/// </para>
/// <para>
/// <b>非執行緒安全</b>：僅供單一寵物的 UI 執行緒使用（雙寵物模式時各自獨立快取，§7.3.6）。
/// </para>
/// </remarks>
public sealed class LruFrameCache<TValue> where TValue : class
{
    /// <summary>預設容量：總計 48 格／隻（§7.3.6）。</summary>
    public const int DefaultFrameCapacity = 48;

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required TValue Value { get; init; }
        public required int Frames { get; init; }
    }

    // 串列頭 = 最近使用（MRU），串列尾 = 最久未使用（LRU，優先淘汰）。
    private readonly LinkedList<Entry> _lru = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _map = new();

    /// <summary>容量上限（格數）。</summary>
    public int FrameCapacity { get; }

    /// <summary>目前快取中的總格數。</summary>
    public int CurrentFrames { get; private set; }

    /// <summary>目前快取中的單元數。</summary>
    public int Count => _map.Count;

    /// <summary>累計呼叫工廠解碼的次數（測試觀測用；驗證「僅首次抽中才解碼」）。</summary>
    public int DecodeCount { get; private set; }

    /// <param name="frameCapacity">容量上限（格數），需 &gt;= 1。預設 <see cref="DefaultFrameCapacity"/>。</param>
    public LruFrameCache(int frameCapacity = DefaultFrameCapacity)
    {
        if (frameCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(frameCapacity), frameCapacity, "容量至少為 1 格。");
        FrameCapacity = frameCapacity;
    }

    /// <summary>
    /// 取得指定鍵的快取值；未命中時以 <paramref name="decode"/> 解碼後放入快取並回傳。
    /// 命中即標記為最近使用；放入前先淘汰最久未使用者直到容納得下
    /// （單一單元格數若超過上限，仍會清空其餘後單獨放入——正在播放的單元必須可渲染）。
    /// </summary>
    /// <param name="key">單元識別鍵（正式使用時為圖片檔案完整路徑，於一隻寵物內唯一）。</param>
    /// <param name="frames">此單元的格數（權重）；&lt; 1 會被視為 1。</param>
    /// <param name="decode">未命中時的解碼工廠（只在未命中時呼叫一次）。</param>
    public TValue Get(string key, int frames, Func<TValue> decode)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(decode);
        if (frames < 1) frames = 1;

        if (_map.TryGetValue(key, out var existing))
        {
            _lru.Remove(existing);
            _lru.AddFirst(existing);   // 標記為 MRU
            return existing.Value.Value;
        }

        // 淘汰 LRU 直到放得下（或已清空）。
        while (CurrentFrames + frames > FrameCapacity && _lru.Last is { } lruNode)
        {
            _lru.RemoveLast();
            _map.Remove(lruNode.Value.Key);
            CurrentFrames -= lruNode.Value.Frames;
        }

        var value = decode();
        DecodeCount++;

        var node = _lru.AddFirst(new Entry { Key = key, Value = value, Frames = frames });
        _map[key] = node;
        CurrentFrames += frames;
        return value;
    }

    /// <summary>是否已快取指定鍵（不改變 LRU 順序）。</summary>
    public bool Contains(string key) => _map.ContainsKey(key);

    /// <summary>清空快取（例如切換圖樣時）。</summary>
    public void Clear()
    {
        _lru.Clear();
        _map.Clear();
        CurrentFrames = 0;
    }
}
