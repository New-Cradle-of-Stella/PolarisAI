namespace Polaris.AI.Authoring;

public enum PaiDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed class PaiDiagnostic
{
    public PaiDiagnostic(string code, PaiDiagnosticSeverity severity, string message,
        string? treeId = null, string? nodeId = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        TreeId = treeId;
        NodeId = nodeId;
    }

    public string Code { get; }
    public PaiDiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string? TreeId { get; }
    public string? NodeId { get; }
    public override string ToString() => $"{Code}: {Message}";
}
