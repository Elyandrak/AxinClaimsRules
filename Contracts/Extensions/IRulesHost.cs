namespace AxinClaimsRules.Contracts.Extensions
{
    /// <summary>
    /// Minimal host surface for extensions (P1.2).
    /// Keep it tiny to preserve stability.
    /// </summary>
    public interface IRulesHost
    {
        void RegisterInfo(string message);

        /// <summary>Register an addon subcommand under /ac.</summary>
        void RegisterAcSubCommand(IAcSubCommand cmd);

        /// <summary>Register an addon flags module.</summary>
        void RegisterFlagsModule(IFlagsModule module);
    }
}
