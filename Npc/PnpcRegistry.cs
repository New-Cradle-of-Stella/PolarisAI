using System;
using System.Collections.Generic;
using Polaris.AI.Registration;

namespace Polaris.AI;

internal static class PnpcRegistry
{
    static readonly Dictionary<string, PnpcSubmission> Definitions =
        new Dictionary<string, PnpcSubmission>(StringComparer.Ordinal);

    internal static void ScanModules()
    {
        foreach ((Type type, PnpcAutoRegistrationAttribute _) in Polaris.PolarisAPI.Types.InPluginsWith<PnpcAutoRegistrationAttribute>())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IPnpcRegistrar).IsAssignableFrom(type)) continue;
            try
            {
                var registrar = (IPnpcRegistrar)Activator.CreateInstance(type, true);
                string owner = type.Assembly.GetName().Name;
                var context = new PnpcRegistrationContext(owner);
                registrar.Register(context);
                foreach (PnpcSubmission submission in context.Submissions) Add(submission);
            }
            catch (Exception ex) { Polaris.PolarisAPI.Errors.Report(ex, $"Registering .pnpc from '{type.FullName}'"); }
        }
    }

    internal static bool TryGet(string id, out PnpcSubmission definition)
        => Definitions.TryGetValue(id ?? string.Empty, out definition!);

    internal static void Clear() => Definitions.Clear();

    static void Add(PnpcSubmission submission)
    {
        string id = submission.Definition.Id;
        if (id == "custom.basic" || id == "citycaster.default" || id == "citycaster.td" || id == "shadow.noel")
            throw new InvalidOperationException($".pnpc id '{id}' is reserved by PolarisAI.");
        if (Definitions.TryGetValue(id, out PnpcSubmission existing))
            throw new InvalidOperationException($".pnpc id '{id}' from '{submission.Owner}' conflicts with '{existing.Owner}'.");
        Definitions.Add(id, submission);
    }
}
