using System.Diagnostics.Tracing;
using System.Text.Json;

internal sealed class NetworkAudit : EventListener
{
    private readonly List<object> events = new();
    public int AttemptCount { get { lock (events) return events.Count; } }

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name is "System.Net.Http" or "System.Net.Sockets" or "System.Net.NameResolution")
            EnableEvents(source, EventLevel.Informational);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventName is not ("RequestStart" or "ConnectStart" or "AcceptStart" or "ResolutionStart"))
            return;
        lock (events)
            events.Add(new { timestamp = DateTimeOffset.UtcNow, source = e.EventSource.Name, name = e.EventName, processId = Environment.ProcessId, payload = e.Payload?.Select(p => p?.ToString()).ToArray() });
    }

    public void Save(string path)
    {
        lock (events)
            File.WriteAllText(path, JsonSerializer.Serialize(new { processId = Environment.ProcessId, attemptCount = events.Count, events }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
