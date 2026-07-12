using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, Inherited = false)]
    public class RemoteAttribute : Attribute
    {
        public string[] Domains { get; private set; }
        public string FullName { get; private set; }

        static readonly Regex StrictIPv4 = new(
@"^(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$",
RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex IPv4Like = new(
            @"^\d+(?:\.\d+){3}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Regex HostName = new(
            @"^(?=.{1,253}\.?$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);


        public bool IsValidFullName()
        {
            return IsValidQualifiedClassName(FullName, true, false); ;
        }

        private static readonly HashSet<string> ReservedKeywords = new HashSet<string>(
    new[]
    {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
    },
    StringComparer.Ordinal);

        public RemoteAttribute(string fullName, params string[] domains)
        {
            Domains = domains;
            FullName = fullName;
        }

        // @TODO: support wildcard records
        public bool AreValidDomains()
        {
            foreach(var domain in Domains)
            {
                if (!IsValidDomain(domain))
                    return false;
            }

            return true;
        }

        private bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            string s = domain.Trim();

            // Accept URI-style IPv6 literals, e.g. [::1]
            if (s.Length > 2 && s[0] == '[' && s[s.Length - 1] == ']')
                s = s.Substring(1, s.Length - 2);

            // Strict IPv4 only: 0.0.0.0 to 255.255.255.255
            if (StrictIPv4.IsMatch(s))
                return true;

            // Reject IPv4-looking strings that failed strict IPv4,
            // e.g. 999.999.999.999
            if (IPv4Like.IsMatch(s))
                return false;

            IPAddress ip;

            // IPv6
            if (IPAddress.TryParse(s, out ip) &&
                ip.AddressFamily == AddressFamily.InterNetworkV6)
                return true;

            // Hostname or domain
            return HostName.IsMatch(s);

        }


        public static bool IsValidQualifiedClassName(string value, bool requireNamespace, bool allowVerbatimIdentifiers)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string s = value.Trim();

            if (s.Length == 0)
                return false;

            // Reject whitespace inside the name
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i]))
                    return false;
            }

            // Optional source-style prefix: global::Namespace.Type
            if (s.StartsWith("global::", StringComparison.Ordinal))
                s = s.Substring("global::".Length);

            string[] parts = s.Split('.');

            if (requireNamespace && parts.Length < 2)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!IsValidIdentifier(parts[i], allowVerbatimIdentifiers))
                    return false;
            }

            return true;
        }

        private static bool IsValidIdentifier(string identifier, bool allowVerbatimIdentifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            string id = identifier;

            if (id[0] == '@')
            {
                if (!allowVerbatimIdentifier)
                    return false;

                id = id.Substring(1);

                if (id.Length == 0)
                    return false;
            }

            if (!IsIdentifierStartCharacter(id[0]))
                return false;

            for (int i = 1; i < id.Length; i++)
            {
                if (!IsIdentifierPartCharacter(id[i]))
                    return false;
            }

            // Reject reserved keywords unless written as @keyword
            if (identifier[0] != '@' && ReservedKeywords.Contains(id))
                return false;

            return true;
        }

        private static bool IsIdentifierStartCharacter(char ch)
        {
            if (ch == '_')
                return true;

            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);

            return category == UnicodeCategory.UppercaseLetter ||
                   category == UnicodeCategory.LowercaseLetter ||
                   category == UnicodeCategory.TitlecaseLetter ||
                   category == UnicodeCategory.ModifierLetter ||
                   category == UnicodeCategory.OtherLetter ||
                   category == UnicodeCategory.LetterNumber;
        }

        private static bool IsIdentifierPartCharacter(char ch)
        {
            if (IsIdentifierStartCharacter(ch))
                return true;

            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);

            return category == UnicodeCategory.DecimalDigitNumber ||
                   category == UnicodeCategory.ConnectorPunctuation ||
                   category == UnicodeCategory.NonSpacingMark ||
                   category == UnicodeCategory.SpacingCombiningMark ||
                   category == UnicodeCategory.Format;
        }

    }
}
