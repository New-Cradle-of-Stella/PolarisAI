using System;
using Polaris.AI.Registration;
using Polaris.Content;

namespace Polaris.AI;

internal static class PnpcRegistry
{
    static readonly ContentCatalog<string, PnpcSubmission> Definitions =
        new ContentCatalog<string, PnpcSubmission>(StringComparer.Ordinal, ContentConflictPolicy.ThrowImmediately);

    internal static void ScanModules()
    {
        ContentRegistrarScanner.ScanAndRun<PnpcAutoRegistrationAttribute, IPnpcRegistrar>(
            (registrar, type) =>
            {
                string owner = type.Assembly.GetName().Name;
                var context = new PnpcRegistrationContext(owner);
                registrar.Register(context);
                foreach (PnpcSubmission submission in context.Submissions) Add(submission);
            },
            (ex, type) => PolarisAPI.Errors.Report(ex, $"Registering .pnpc from '{type.FullName}'"));
    }

    internal static bool TryGet(string id, out PnpcSubmission definition)
        => Definitions.TryGet(id ?? string.Empty, out definition!);

    internal static void Clear() => Definitions.Clear();

    static void Add(PnpcSubmission submission)
    {
        string id = submission.Definition.Id;
        if (BuiltInNpcIds.IsReserved(id))
            throw new InvalidOperationException($".pnpc id '{id}' is reserved by PolarisAI.");
        Definitions.TryRegister(id, submission, submission.Owner);
    }
}
