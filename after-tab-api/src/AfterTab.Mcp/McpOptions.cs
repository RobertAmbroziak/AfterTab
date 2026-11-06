namespace AfterTab.Mcp;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";
    public const string AuthenticationScheme = "AfterTabMcp";
    public const string ReadPolicy = "AfterTabMcpRead";
    public const string ScopeClaim = "aftertab_mcp_scope";
    public const string ReadScope = "aftertab.read";
    public const string WriteScope = "aftertab.write";
    public bool Enabled { get; set; }
    public string? PublicOrigin { get; set; }
}
