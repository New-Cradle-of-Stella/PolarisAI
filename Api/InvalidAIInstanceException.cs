using System;

namespace Polaris.AI;

public sealed class InvalidAIInstanceException : InvalidOperationException
{
    internal InvalidAIInstanceException(string what)
        : base($"This AI instance is no longer valid: {what}. It was detached, despawned, or released by a map change.")
    {
    }
}
