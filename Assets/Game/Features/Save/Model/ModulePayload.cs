using Newtonsoft.Json.Linq;

namespace Save.Model
{
    /// <summary>
    /// Per-module payload wrapper inside SaveData. <see cref="Json"/> stores the module's state as a
    /// <see cref="JToken"/> so that the outer SaveData serializer emits the inner object inline
    /// (a nested JSON object), not as an escaped string. Legacy saves where Json was a string are
    /// still readable — see <c>SaveService.GetModuleAsync</c>.
    /// </summary>
    public sealed class ModulePayload
    {
        public int Version { get; set; }
        public JToken Json { get; set; }
    }
}
