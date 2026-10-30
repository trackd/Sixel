namespace Sixel.Protocols;

// Preserve support for .NET Framework, which lacks these throw helpers.
#pragma warning disable CA1510, CA1513

/// <summary>
/// An explicit client for applications that own terminal IO. Never reads Console.
/// The exchange callback must write the request and route one complete matching
/// graphics response, preserving unrelated input and respecting the timeout.
/// </summary>
public sealed class KittySession : IDisposable {
    private readonly Action<string> write;
    private readonly Func<string, TimeSpan, string> exchange;
    private readonly HashSet<uint> ownedImages = [];
    private bool disposed;
    public bool Tmux { get; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);
    public uint[] OwnedImageIds => ownedImages.ToArray();

    public KittySession(Action<string> write, Func<string, TimeSpan, string> exchange, bool tmux = false) {
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        this.exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        Tmux = tmux;
    }

    /// <summary>Uploads, queries, or places an image and validates its acknowledgement.</summary>
    public KittyResponse Execute(IEnumerable<string> control, byte[]? payload = null) =>
        ExecuteCore(control, entries => KittyProtocol.Encode(entries, payload, Tmux));

    /// <summary>Transfers a caller-owned resource; its lifetime extends through acknowledgement.</summary>
    public KittyResponse ExecuteTransfer(IEnumerable<string> control, KittyTransfer transfer) {
        if (transfer is null) throw new ArgumentNullException(nameof(transfer));
        return ExecuteCore(control, entries => transfer.Encode(entries, Tmux));
    }

    private KittyResponse ExecuteCore(IEnumerable<string> control, Func<IEnumerable<string>, string> encode) {
        CheckDisposed();
        if (Timeout <= TimeSpan.Zero) throw new InvalidOperationException("Timeout must be positive.");
        Dictionary<char, string> values = KittyProtocol.Parse(control);
        char action = values.TryGetValue('a', out string? value) ? value[0] : 't';
        if (action is not ('t' or 'T' or 'q' or 'p'))
            throw new ArgumentException("Only upload, query, and placement actions acknowledge success. Use Send for other actions.", nameof(control));
        uint id = Number(values, 'i'), number = Number(values, 'I'), placement = Number(values, 'p');
        if (id == 0 && number == 0)
            throw new ArgumentException("Acknowledgements require a nonzero image ID or image number.", nameof(control));
        values['q'] = "0";
        string request = encode(values.Select(pair => pair.Key + "=" + pair.Value));
        string packet = exchange(request, Timeout);
        if (!KittyResponse.TryParse(packet, out KittyResponse? response) || response is null)
            throw new IOException("No complete Kitty acknowledgement was returned; terminal state is unknown.");
        if ((id != 0 && response.ImageId != id) || (number != 0 && response.ImageNumber != number) ||
            (placement != 0 && response.PlacementId != placement))
            throw new IOException("The transport returned an acknowledgement for another operation.");
        if (response.Success && action is 't' or 'T' && response.ImageId != 0)
            ownedImages.Add(response.ImageId);
        return response;
    }

    /// <summary>Sends any command silently. Does not claim it succeeded or acquire image ownership.</summary>
    public void Send(IEnumerable<string> control, byte[]? payload = null) {
        CheckDisposed();
        Dictionary<char, string> values = KittyProtocol.Parse(control);
        values['q'] = "2";
        write(KittyProtocol.Encode(values.Select(pair => pair.Key + "=" + pair.Value), payload, Tmux));
    }

    /// <summary>Stops tracking an image without deleting it, for ownership transfer to another client.</summary>
    public bool Release(uint imageId) {
        CheckDisposed();
        return ownedImages.Remove(imageId);
    }

    /// <summary>Deletes only images successfully uploaded by this session, using d=I.</summary>
    public void Dispose() {
        if (disposed) return;
        foreach (uint id in ownedImages.ToArray()) {
            write(KittyProtocol.Encode(["a=d", "d=I", KittyImage.Number('i', id)], tmux: Tmux));
            ownedImages.Remove(id);
        }
        disposed = true;
    }

    private static uint Number(Dictionary<char, string> values, char key) =>
        values.TryGetValue(key, out string? value) ? uint.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : 0;

    private void CheckDisposed() {
        if (disposed) throw new ObjectDisposedException(nameof(KittySession));
    }
}
