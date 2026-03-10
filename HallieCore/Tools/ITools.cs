namespace Hallie.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<string> ExecuteAsync(Dictionary<string, object> parameters);
        ToolParameter[] GetParameters();
    }

    public class ToolParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // "string", "number", "boolean"
        public string Description { get; set; } = "";
        public bool Required { get; set; }
    }
}
