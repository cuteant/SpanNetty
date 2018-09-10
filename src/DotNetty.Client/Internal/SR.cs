using System;
using System.Resources;

namespace DotNetty.Client.Internal
{
  internal sealed partial class SR : Strings
  {
    // Needed for debugger integration
    internal static string GetResourceString(string resourceKey)
    {
      return GetResourceString(resourceKey, String.Empty);
    }

    internal static string GetResourceString(string resourceKey, string defaultString)
    {
      string resourceString = null;
      try { resourceString = ResourceManager.GetString(resourceKey, null); }
      catch (MissingManifestResourceException) { }

      if (defaultString != null && resourceKey.Equals(resourceString, StringComparison.Ordinal))
      {
        return defaultString;
      }

      return resourceString;
    }

    internal static string Format(string resourceFormat, params object[] args)
    {
      if (args != null)
      {
        return String.Format(resourceFormat, args);
      }

      return resourceFormat;
    }

    internal static string Format(string resourceFormat, object p1)
    {
      return String.Format(resourceFormat, p1);
    }

    internal static string Format(string resourceFormat, object p1, object p2)
    {
      return String.Format(resourceFormat, p1, p2);
    }

    internal static string Format(string resourceFormat, object p1, object p2, object p3)
    {
      return String.Format(resourceFormat, p1, p2, p3);
    }
  }
}
