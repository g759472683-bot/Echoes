using System.Collections.Generic;

/// <summary>
/// Narrow data interface consumed by MultiEndingSystem for chapter ending
/// configuration lookups (ADR-0010).
///
/// Implemented by the chapter management system (#15). This interface is
/// intentionally narrow — it only exposes what the multi-ending system needs.
/// </summary>
public interface IEndingDefinitionProvider
{
    /// <summary>
    /// Returns the EndingDefinition array for a given chapter.
    /// Returns null or empty if the chapter has no configured endings.
    /// </summary>
    List<EndingDefinition> GetEndingDefinitions(string chapterKey);
}
