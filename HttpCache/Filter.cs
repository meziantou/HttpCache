using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fiddler;
using Meziantou.Framework;

namespace HttpCache
{
    internal class Filter
    {
        private readonly string[] _methods;
        private readonly Regex _regex;

        public Filter(string pattern)
        {
            pattern = pattern.Trim();
            var index = pattern.IndexOf(' ');
            if (index > 0)
            {
                var method = pattern.Substring(0, index);
                if (string.IsNullOrEmpty(method) || method == "*")
                {
                    _methods = null;
                }
                else
                {
                    _methods = method.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                }

                pattern = pattern.Substring(index).TrimStart();
            }

            _regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public bool Match(Session session)
        {
            if (session.RequestMethod.EqualsIgnoreCase("CONNECT"))
                return false;

            if (_methods != null && _methods.All(m => !m.EqualsIgnoreCase(session.RequestMethod)))
                return false;

            return _regex.IsMatch(session.fullUrl);
        }
    }
}
