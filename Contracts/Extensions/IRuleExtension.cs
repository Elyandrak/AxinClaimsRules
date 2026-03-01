using Vintagestory.API.Server;

namespace AxinClaimsRules.Contracts.Extensions
{
    /// <summary>
    /// Implement this in an expansion mod (plugin) to extend AxinClaimsRules.
    /// </summary>
    public interface IRuleExtension
    {
        string Id { get; }
        void Register(IRulesHost host, ICoreServerAPI api);
    }
}
