using System.Linq;
using Common.Localization.Providers;

namespace Common.Localization.Entries;

public class EntryFormatted : EntryBase
{
    private object[] _arguments;
    private IEntry _entry;

    public EntryFormatted(IEntry entry, params object[] arguments)
    {
        _entry = entry;
        _arguments = arguments;
    }

    public override string Get(ILocalizationProvider provider, params object[] additionalArgs)
    {
        return _entry.Get(provider, _arguments.Concat(additionalArgs).ToArray());
    }
}