// Compiled into both the library and PureMarkup.Blazor.Generators (linked), so the generator and runtime agree.
using System.Text;

namespace PureMarkup.Blazor.Styling;

/// <summary>Class and custom-property names for <c>[Stylesheet]</c> members: <c>{Type}_{Member}-{hash}</c>, or <c>pm-{hash}</c> in Release.</summary>
internal static class CssNames
{
    /// <summary>6 base-36 characters of FNV-1a 64 over <c>assembly|type|member</c> (UTF-8).</summary>
    public static string Hash(string assembly, string fullTypeName, string member)
    {
        const ulong offset = 14695981039346656037, prime = 1099511628211;
        var h = offset;
        foreach (var b in Encoding.UTF8.GetBytes(assembly + "|" + fullTypeName + "|" + member))
        {
            h ^= b;
            h *= prime;
        }
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var chars = new char[6];
        for (var i = 5; i >= 0; i--)
        {
            chars[i] = digits[(int)(h % 36)];
            h /= 36;
        }
        return new string(chars);
    }

    /// <summary>The class name for a rule (no leading dot). <paramref name="typeName"/> is the short type name.</summary>
    public static string For(string assembly, string fullTypeName, string typeName, string member, bool readable) =>
        (readable ? typeName + "_" + member + "-" : "pm-") + Hash(assembly, fullTypeName, member);
}
