using System;
using System.Collections.Generic;
using System.Text;

namespace NSQLFormatter
{
    public class Formatter
    {
        public const string WhiteSpace = " \n\r\f\t";

        protected const string IndentString = "    ";
        protected const string Initial = "\n    ";
        protected static readonly HashSet<string> beginClauses = new HashSet<string>();
        protected static readonly HashSet<string> dml = new HashSet<string>();
        protected static readonly HashSet<string> endClauses = new HashSet<string>();
        protected static readonly HashSet<string> logical = new HashSet<string>();
        protected static readonly HashSet<string> misc = new HashSet<string>();
        protected static readonly HashSet<string> quantifiers = new HashSet<string>();

        static Formatter()
        {
            beginClauses.Add("left");
            beginClauses.Add("right");
            beginClauses.Add("inner");
            beginClauses.Add("outer");
            beginClauses.Add("group");
            beginClauses.Add("order");

            endClauses.Add("where");
            endClauses.Add("set");
            endClauses.Add("having");
            endClauses.Add("join");
            endClauses.Add("from");
            endClauses.Add("by");
            endClauses.Add("join");
            endClauses.Add("into");
            endClauses.Add("union");

            logical.Add("and");
            logical.Add("or");
            logical.Add("when");
            logical.Add("else");
            logical.Add("end");

            quantifiers.Add("in");
            quantifiers.Add("all");
            quantifiers.Add("exists");
            quantifiers.Add("some");
            quantifiers.Add("any");

            dml.Add("insert");
            dml.Add("update");
            dml.Add("delete");

            misc.Add("select");
            misc.Add("on");
        }

        public static string Format(string source)
        {
            return new FormatProcess(source).Perform();
        }

        #region Nested type: FormatProcess

        private class FormatProcess
        {
            private readonly List<bool> afterByOrFromOrSelects = new List<bool>();
            private readonly List<int> parenCounts = new List<int>();
            private readonly StringBuilder result = new StringBuilder();
            private readonly IEnumerator<string> tokens;
            private bool afterBeginBeforeEnd;
            private bool afterBetween;
            private bool afterByOrSetOrFromOrSelect;
            private bool afterInsert;
            private bool afterOn;
            private bool beginLine = true;
            private bool endCommandFound;

            private int indent = 1;
            private int inFunction;

            private string lastToken;
            private string lcToken;
            private int parensSinceSelect;
            private string token;

            public FormatProcess(string sql)
            {
                // TODO : some delimiter may depend from a specific Dialect/Drive (as ';' to separate multi query)
                tokens = new StringTokenizer(sql, "()+*/-=<>'`\"[],;" + WhiteSpace, true).GetEnumerator();
            }

            public string Perform()
            {
                result.Append(Initial);

                while (tokens.MoveNext())
                {
                    token = tokens.Current;
                    lcToken = token.ToLowerInvariant();

                    if ("'".Equals(token))
                    {
                        ExtractStringEnclosedBy("'");
                    }
                    else if ("\"".Equals(token))
                    {
                        ExtractStringEnclosedBy("\"");
                    }

                    if (IsMultiQueryDelimiter(token))
                    {
                        StartingNewQuery();
                    }
                    else if (afterByOrSetOrFromOrSelect && ",".Equals(token))
                    {
                        CommaAfterByOrFromOrSelect();
                    }
                    else if (afterOn && ",".Equals(token))
                    {
                        CommaAfterOn();
                    }
                    else if ("(".Equals(token))
                    {
                        OpenParen();
                    }
                    else if (")".Equals(token))
                    {
                        CloseParen();
                    }
                    else if (beginClauses.Contains(lcToken))
                    {
                        BeginNewClause();
                    }
                    else if (endClauses.Contains(lcToken))
                    {
                        EndNewClause();
                    }
                    else if ("select".Equals(lcToken))
                    {
                        Select();
                    }
                    else if (dml.Contains(lcToken))
                    {
                        UpdateOrInsertOrDelete();
                    }
                    else if ("values".Equals(lcToken))
                    {
                        Values();
                    }
                    else if ("on".Equals(lcToken))
                    {
                        On();
                    }
                    else if (afterBetween && lcToken.Equals("and"))
                    {
                        Misc();
                        afterBetween = false;
                    }
                    else if (logical.Contains(lcToken))
                    {
                        Logical();
                    }
                    else if (IsWhitespace(token))
                    {
                        White();
                    }
                    else
                    {
                        Misc();
                    }

                    if (!IsWhitespace(token))
                    {
                        lastToken = lcToken;
                    }
                }
                return result.ToString();
            }

            private void StartingNewQuery()
            {
                Out();
                indent = 1;
                endCommandFound = true;
                Newline();
            }

            private bool IsMultiQueryDelimiter(string delimiter)
            {
                return ";".Equals(delimiter);
            }

            private void ExtractStringEnclosedBy(string stringDelimiter)
            {
                while (tokens.MoveNext())
                {
                    string t = tokens.Current;
                    token += t;
                    if (stringDelimiter.Equals(t))
                    {
                        break;
                    }
                }
            }

            private void CommaAfterOn()
            {
                Out();
                indent--;
                Newline();
                afterOn = false;
                afterByOrSetOrFromOrSelect = true;
            }

            private void CommaAfterByOrFromOrSelect()
            {
                Out();
                Newline();
            }

            private void Logical()
            {
                if ("end".Equals(lcToken))
                {
                    indent--;
                }
                Newline();
                Out();
                beginLine = false;
            }

            private void On()
            {
                indent++;
                afterOn = true;
                Newline();
                Out();
                beginLine = false;
            }

            private void Misc()
            {
                Out();
                if ("between".Equals(lcToken))
                {
                    afterBetween = true;
                }
                if (afterInsert)
                {
                    Newline();
                    afterInsert = false;
                }
                else
                {
                    beginLine = false;
                    if ("case".Equals(lcToken))
                    {
                        indent++;
                    }
                }
            }

            private void White()
            {
                if (!beginLine)
                {
                    result.Append(" ");
                }
            }

            private void UpdateOrInsertOrDelete()
            {
                Out();
                indent++;
                beginLine = false;
                if ("update".Equals(lcToken))
                {
                    Newline();
                }
                if ("insert".Equals(lcToken))
                {
                    afterInsert = true;
                }
                endCommandFound = false;
            }

            private void Select()
            {
                Out();
                indent++;
                Newline();
                parenCounts.Insert(parenCounts.Count, parensSinceSelect);
                afterByOrFromOrSelects.Insert(afterByOrFromOrSelects.Count, afterByOrSetOrFromOrSelect);
                parensSinceSelect = 0;
                afterByOrSetOrFromOrSelect = true;
                endCommandFound = false;
            }

            private void Out()
            {
                result.Append(token);
            }

            private void EndNewClause()
            {
                if (!afterBeginBeforeEnd)
                {
                    indent--;
                    if (afterOn)
                    {
                        indent--;
                        afterOn = false;
                    }
                    Newline();
                }
                Out();
                if (!"union".Equals(lcToken))
                {
                    indent++;
                }
                Newline();
                afterBeginBeforeEnd = false;
                afterByOrSetOrFromOrSelect = "by".Equals(lcToken) || "set".Equals(lcToken) || "from".Equals(lcToken);
            }

            private void BeginNewClause()
            {
                if (!afterBeginBeforeEnd)
                {
                    if (afterOn)
                    {
                        indent--;
                        afterOn = false;
                    }
                    indent--;
                    Newline();
                }
                Out();
                beginLine = false;
                afterBeginBeforeEnd = true;
            }

            private void Values()
            {
                indent--;
                Newline();
                Out();
                indent++;
                Newline();
            }

            private void CloseParen()
            {
                if (endCommandFound)
                {
                    Out();
                    return;
                }
                parensSinceSelect--;
                if (parensSinceSelect < 0)
                {
                    indent--;
                    int tempObject = parenCounts[parenCounts.Count - 1];
                    parenCounts.RemoveAt(parenCounts.Count - 1);
                    parensSinceSelect = tempObject;

                    bool tempObject2 = afterByOrFromOrSelects[afterByOrFromOrSelects.Count - 1];
                    afterByOrFromOrSelects.RemoveAt(afterByOrFromOrSelects.Count - 1);
                    afterByOrSetOrFromOrSelect = tempObject2;
                }
                if (inFunction > 0)
                {
                    inFunction--;
                    Out();
                }
                else
                {
                    if (!afterByOrSetOrFromOrSelect)
                    {
                        indent--;
                        Newline();
                    }
                    Out();
                }
                beginLine = false;
            }

            private void OpenParen()
            {
                if (endCommandFound)
                {
                    Out();
                    return;
                }
                if (IsFunctionName(lastToken) || inFunction > 0)
                {
                    inFunction++;
                }
                beginLine = false;
                if (inFunction > 0)
                {
                    Out();
                }
                else
                {
                    Out();
                    if (!afterByOrSetOrFromOrSelect)
                    {
                        indent++;
                        Newline();
                        beginLine = true;
                    }
                }
                parensSinceSelect++;
            }

            private static bool IsFunctionName(string tok)
            {
                char begin = tok[0];
                bool isIdentifier = (char.IsLetter(begin) || begin.CompareTo('$') == 0 || begin.CompareTo('_') == 0) || '"' == begin;
                return isIdentifier && !logical.Contains(tok) && !endClauses.Contains(tok) && !quantifiers.Contains(tok)
                       && !dml.Contains(tok) && !misc.Contains(tok);
            }

            private static bool IsWhitespace(string token)
            {
                return WhiteSpace.IndexOf(token) >= 0;
            }

            private void Newline()
            {
                result.Append("\n");
                for (int i = 0; i < indent; i++)
                {
                    result.Append(IndentString);
                }
                beginLine = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// A StringTokenizer java like object 
    /// </summary>
    public class StringTokenizer : IEnumerable<string>
    {
        private const string _defaultDelim = " \t\n\r\f";
        private string _origin;
        private string _delim;
        private bool _returnDelim;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        public StringTokenizer(string str)
        {
            _origin = str;
            _delim = _defaultDelim;
            _returnDelim = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="delim"></param>
        public StringTokenizer(string str, string delim)
        {
            _origin = str;
            _delim = delim;
            _returnDelim = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="delim"></param>
        /// <param name="returnDelims"></param>
        public StringTokenizer(string str, string delim, bool returnDelims)
        {
            _origin = str;
            _delim = delim;
            _returnDelim = returnDelims;
        }

        #region IEnumerable<string> Members

        public IEnumerator<string> GetEnumerator()
        {
            return new StringTokenizerEnumerator(this);
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new StringTokenizerEnumerator(this);
        }

        #endregion


        private class StringTokenizerEnumerator : IEnumerator<string>
        {
            private StringTokenizer _stokenizer;
            private int _cursor = 0;
            private String _next = null;

            public StringTokenizerEnumerator(StringTokenizer stok)
            {
                _stokenizer = stok;
            }

            #region IEnumerator<string> Members

            public string Current
            {
                get { return _next; }
            }

            #endregion

            #region IDisposable Members

            public void Dispose()
            {
            }

            #endregion

            #region IEnumerator Members

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _next = GetNext();
                return _next != null;
            }

            public void Reset()
            {
                _cursor = 0;
            }

            #endregion

            private string GetNext()
            {
                char c;
                bool isDelim;

                if (_cursor >= _stokenizer._origin.Length)
                    return null;

                c = _stokenizer._origin[_cursor];
                isDelim = (_stokenizer._delim.IndexOf(c) != -1);

                if (isDelim)
                {
                    _cursor++;
                    if (_stokenizer._returnDelim)
                    {
                        return c.ToString();
                    }
                    return GetNext();
                }

                int nextDelimPos = _stokenizer._origin.IndexOfAny(_stokenizer._delim.ToCharArray(), _cursor);
                if (nextDelimPos == -1)
                {
                    nextDelimPos = _stokenizer._origin.Length;
                }

                string nextToken = _stokenizer._origin.Substring(_cursor, nextDelimPos - _cursor);
                _cursor = nextDelimPos;
                return nextToken;
            }
        }
    }

}
