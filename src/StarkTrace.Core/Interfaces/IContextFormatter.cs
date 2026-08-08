using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

/// <summary>
/// Formats context tier data into either ACL or Prose format.
/// Two implementations: AclFormatter and ProseFormatter.
/// </summary>
public interface IContextFormatter
{
    string FormatP1(CurrentStateContext state);
    string FormatP2(List<SemanticMemory> memories);
    string FormatP3(List<AntiPattern> antiPatterns);
    string FormatP4(List<Preference> preferences);
    string FormatP5(ProjectFacts facts);
}
