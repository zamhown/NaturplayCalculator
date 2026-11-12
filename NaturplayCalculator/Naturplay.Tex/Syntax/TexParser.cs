using System;
using System.Collections.Generic;
using System.Linq;

namespace Naturplay.Tex
{
    /// <summary>Bounded recursive descent for a documented math-TeX subset.
    /// Braces group syntax; only explicit delimiters draw parentheses.</summary>
    public sealed class TexParser
    {
        private string source;
        private int position, depth, nodes;
        private char rowTerminator;
        private bool rowEnvironment, rowFence;
        private static readonly HashSet<string> operators = new HashSet<string>(new[]
        { "sin", "cos", "tan", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "ln", "log", "lg", "exp", "lim", "min", "max", "det", "gcd" });
        private static readonly Dictionary<string, string> symbols = new Dictionary<string, string>
        {
            ["alpha"]="α", ["beta"]="β", ["gamma"]="γ", ["delta"]="δ", ["epsilon"]="ε", ["varepsilon"]="ϵ",
            ["zeta"]="ζ", ["eta"]="η", ["theta"]="θ", ["vartheta"]="ϑ", ["iota"]="ι", ["kappa"]="κ",
            ["lambda"]="λ", ["mu"]="μ", ["nu"]="ν", ["xi"]="ξ", ["pi"]="π", ["varpi"]="ϖ",
            ["rho"]="ρ", ["varrho"]="ϱ", ["sigma"]="σ", ["varsigma"]="ς", ["tau"]="τ", ["upsilon"]="υ",
            ["phi"]="ϕ", ["varphi"]="φ", ["chi"]="χ", ["psi"]="ψ", ["omega"]="ω",
            ["Gamma"]="Γ", ["Delta"]="Δ", ["Theta"]="Θ", ["Lambda"]="Λ", ["Xi"]="Ξ", ["Pi"]="Π",
            ["Sigma"]="Σ", ["Upsilon"]="Υ", ["Phi"]="Φ", ["Psi"]="Ψ", ["Omega"]="Ω",
            ["infty"]="∞", ["partial"]="∂", ["nabla"]="∇", ["ell"]="ℓ", ["hbar"]="ℏ",
            ["cdot"]="⋅", ["times"]="×", ["div"]="÷", ["pm"]="±", ["mp"]="∓",
            ["le"]="≤", ["leq"]="≤", ["ge"]="≥", ["geq"]="≥", ["ne"]="≠", ["neq"]="≠",
            ["approx"]="≈", ["equiv"]="≡", ["in"]="∈", ["notin"]="∉", ["subset"]="⊂",
            ["to"]="→", ["rightarrow"]="→", ["leftarrow"]="←", ["leftrightarrow"]="↔",
            ["cdots"]="⋯", ["ldots"]="…", ["vdots"]="⋮", ["ddots"]="⋱", ["prime"]="′",
            ["sum"]="∑", ["prod"]="∏", ["int"]="∫", ["iint"]="∬", ["oint"]="∮",
            ["langle"]="⟨", ["rangle"]="⟩", ["lfloor"]="⌊", ["rfloor"]="⌋", ["lceil"]="⌈", ["rceil"]="⌉",
            ["lbrace"]="{", ["rbrace"]="}", ["vert"]="|", ["Vert"]="‖", ["|"]="‖",
            ["{"]="{", ["}"]="}", ["%"]="%", ["_"]="_", ["#"]="#", ["&"]="&", ["$"]="$",
            ["backslash"]="\\", ["degree"]="°", ["square"]="□"
        };

        public TexDocument Parse(string latex)
        {
            if (latex == null) throw new ArgumentNullException(nameof(latex));
            if (latex.Length > TexLimits.MaxSourceLength) throw new TexParseException("Formula is too long.", 0);
            source = latex; position = depth = nodes = 0;
            rowTerminator = '\0'; rowEnvironment = rowFence = false;
            var root = ParseRow();
            SkipWhite();
            if (position != source.Length) Fail("Unexpected closing token.");
            return new TexDocument(source, root);
        }

        private TexNode Node(TexKind kind, int start, string text = "", IEnumerable<TexNode> children = null,
            TexAtomClass atomClass = TexAtomClass.Ordinary, TexFont font = TexFont.Roman,
            string closing = "", int rows = 0, int columns = 0, double spaceEm = 0,
            bool limits = false, TexSpan? contentSpan = null)
        {
            if (++nodes > TexLimits.MaxNodes) Fail("Formula contains too many nodes.");
            return new TexNode(kind, new TexSpan(start, position - start), text, children, atomClass, font,
                closing, rows, columns, spaceEm, limits, contentSpan);
        }

        private TexNode ParseRow(char terminator = '\0', bool environment = false, bool fence = false)
        {
            if (++depth > TexLimits.MaxDepth) Fail("Formula nesting is too deep.");
            char previousTerminator = rowTerminator;
            bool previousEnvironment = rowEnvironment, previousFence = rowFence;
            rowTerminator = terminator; rowEnvironment = environment; rowFence = fence;
            int start = position;
            var items = new List<TexNode>();
            while (true)
            {
                SkipWhite();
                if (position >= source.Length || terminator != '\0' && source[position] == terminator
                    || environment && (source[position] == '&' || AtCommand("\\") || AtCommand("end"))
                    || fence && AtCommand("right")) break;
                if (source[position] == '}' || source[position] == '&' || AtCommand("right") || AtCommand("end"))
                    Fail("Unexpected closing token.");
                var atom = ParseAtom();
                items.Add(ParseScripts(atom));
            }
            depth--;
            rowTerminator = previousTerminator; rowEnvironment = previousEnvironment; rowFence = previousFence;
            return Node(TexKind.Row, start, children: items);
        }

        private TexNode ParseAtom()
        {
            SkipWhite();
            if (position >= source.Length) Fail("Expected an atom.");
            int start = position;
            char c = source[position++];
            if (c == '{')
            {
                int bodyStart = position;
                var body = ParseRow('}');
                Require('}');
                return Node(TexKind.Row, start, children: body.Children, contentSpan: new TexSpan(bodyStart, position - bodyStart - 1));
            }
            if (c == '\\') return ParseCommand(start);
            if ("^_}$&".IndexOf(c) >= 0) Fail("Unexpected token '" + c + "'.");
            if (char.IsHighSurrogate(c))
            {
                if (position >= source.Length || !char.IsLowSurrogate(source[position])) Fail("Invalid Unicode scalar.");
                position++;
            }
            else if (char.IsLowSurrogate(c)) Fail("Invalid Unicode scalar.");
            string text = source.Substring(start, position - start);
            return Node(TexKind.Glyph, start, text, atomClass: Classify(text),
                font: char.IsLetter(c) ? TexFont.MathItalic : TexFont.Roman);
        }

        private TexNode ParseScripts(TexNode atom)
        {
            TexNode sup = null, sub = null;
            bool limits = atom.Limits;
            while (true)
            {
                SkipWhite();
                if (AtCommand("limits") || AtCommand("nolimits"))
                {
                    if (atom.AtomClass != TexAtomClass.Operator) Fail("Limits require an operator.");
                    position++; limits = ReadCommand() == "limits"; continue;
                }
                if (position >= source.Length || source[position] != '^' && source[position] != '_') break;
                char script = source[position++];
                if (script == '^' ? sup != null : sub != null) Fail("Duplicate script.");
                var value = ParseArgument();
                if (script == '^') sup = value; else sub = value;
            }
            if (sup == null && sub == null) return atom;
            var empty = new TexNode(TexKind.Row, new TexSpan(position, 0));
            return Node(TexKind.Scripts, atom.Span.Start, children: new[] { atom, sup ?? empty, sub ?? empty },
                atomClass: atom.AtomClass, limits: limits);
        }

        private TexNode ParseArgument()
        {
            if (++depth > TexLimits.MaxDepth) Fail("Formula nesting is too deep.");
            var result = ParseAtom();
            depth--;
            return result;
        }

        private TexNode ParseCommand(int start)
        {
            string command = ReadCommand();
            if (command == "phantom") return Node(TexKind.Phantom, start, children: new[] { ParseArgument() });
            if (command == "frac" || command == "dfrac" || command == "tfrac" || command == "binom")
            {
                var numerator = ParseArgument();
                var denominator = ParseArgument();
                return Node(TexKind.Fraction, start, command, new[] { numerator, denominator }, TexAtomClass.Inner);
            }
            if (command == "sqrt")
            {
                SkipWhite(); TexNode degree = null;
                if (position < source.Length && source[position] == '[')
                { position++; degree = ParseRow(']'); Require(']'); }
                var body = ParseArgument();
                return Node(TexKind.Radical, start, children: degree == null ? new[] { body } : new[] { body, degree });
            }
            if (command == "left")
            {
                string left = ReadDelimiter();
                var body = ParseRow(fence: true);
                if (!AtCommand("right")) Fail("Missing \\right.");
                position++; ReadCommand();
                string right = ReadDelimiter();
                return Node(TexKind.Delimited, start, left, new[] { body }, TexAtomClass.Inner, closing: right);
            }
            if (command == "begin") return ParseEnvironment(start);
            if (command == "text" || command == "operatorname")
                return Node(TexKind.Glyph, start, ReadTextGroup(), atomClass: command == "operatorname" ? TexAtomClass.Operator : TexAtomClass.Ordinary);
            if (command == "mathrm" || command == "mathit" || command == "mathbf" || command == "mathnormal"
                || command == "displaystyle" || command == "textstyle" || command == "scriptstyle" || command == "scriptscriptstyle")
            {
                bool declaration = command.EndsWith("style", StringComparison.Ordinal);
                var body = declaration ? ParseRow(rowTerminator, rowEnvironment, rowFence) : ParseArgument();
                return Node(TexKind.Styled, start, command, new[] { body },
                    font: command == "mathit" || command == "mathnormal" ? TexFont.MathItalic : command == "mathbf" ? TexFont.Bold : TexFont.Roman);
            }
            if (command == "overline" || command == "underline" || command == "hat" || command == "bar" || command == "vec")
                return Node(TexKind.Accent, start, command, new[] { ParseArgument() });
            double spacing = command == "," ? 1.0 / 6 : command == ":" ? 2.0 / 9 : command == ";" ? 5.0 / 18
                : command == "!" ? -1.0 / 6 : command == "quad" ? 1 : command == "qquad" ? 2 : command == " " ? 1.0 / 3 : double.NaN;
            if (!double.IsNaN(spacing)) return Node(TexKind.Space, start, spaceEm: spacing);
            if (command == "bmod") return Node(TexKind.Glyph, start, "mod", atomClass: TexAtomClass.Binary);
            if (operators.Contains(command)) return Node(TexKind.Glyph, start, command, atomClass: TexAtomClass.Operator, limits: command == "lim");
            if (symbols.TryGetValue(command, out var symbol))
                return Node(TexKind.Glyph, start, symbol, atomClass: Classify(symbol),
                    font: symbol.Length == 1 && char.IsLower(symbol[0]) ? TexFont.MathItalic : TexFont.Roman,
                    limits: command == "sum" || command == "prod");
            throw new TexParseException("Unsupported command \\" + command + ".", start);
        }

        private TexNode ParseEnvironment(int start)
        {
            string name = ReadTextGroup();
            if (!new[] { "matrix", "pmatrix", "bmatrix", "Bmatrix", "vmatrix", "Vmatrix", "cases", "aligned", "array" }.Contains(name))
                Fail("Unsupported math environment.");
            string alignment = name == "array" ? ReadTextGroup() : "";
            if (alignment.Any(c => c != 'l' && c != 'c' && c != 'r' && c != '|' && !char.IsWhiteSpace(c))) Fail("Invalid array alignment.");
            alignment = string.Concat(alignment.Where(c => !char.IsWhiteSpace(c)));
            var cells = new List<TexNode>();
            int rows = 0, columns = -1, current = 0;
            while (true)
            {
                cells.Add(ParseRow(environment: true)); current++;
                if (current > TexLimits.MaxMatrixDimension) Fail("Too many matrix columns.");
                if (position < source.Length && source[position] == '&') { position++; continue; }
                if (columns < 0) columns = current;
                if (columns != current) Fail("Matrix rows have different column counts.");
                rows++; current = 0;
                if (rows > TexLimits.MaxMatrixDimension) Fail("Too many matrix rows.");
                if (AtCommand("\\"))
                {
                    position++; ReadCommand(); SkipWhite();
                    if (!AtCommand("end")) continue;
                }
                if (!AtCommand("end")) Fail("Missing environment end.");
                position++; ReadCommand();
                if (ReadTextGroup() != name) Fail("Mismatched environment end.");
                break;
            }
            if (name == "array" && alignment.Count(c => c != '|') != columns) Fail("Array alignment must specify each column.");
            if (name == "cases" && columns != 2) Fail("Cases requires two columns.");
            return Node(TexKind.Matrix, start, name, cells, TexAtomClass.Inner, closing: alignment, rows: rows, columns: columns);
        }

        private string ReadDelimiter()
        {
            SkipWhite();
            if (position >= source.Length) Fail("Expected a delimiter.");
            string value = source[position++].ToString();
            if (value == "\\")
            {
                string command = ReadCommand();
                if (!symbols.TryGetValue(command, out value)) Fail("Unknown delimiter.");
            }
            if (!new[] { "(", ")", "[", "]", "{", "}", "|", "‖", "⟨", "⟩", "⌊", "⌋", "⌈", "⌉", ".", "/" }.Contains(value))
                Fail("Invalid delimiter.");
            return value == "." ? "" : value;
        }

        private string ReadTextGroup()
        {
            SkipWhite(); Require('{');
            var text = new System.Text.StringBuilder();
            while (position < source.Length && source[position] != '}')
            {
                char c = source[position++];
                if (c == '{') Fail("Nested text groups are not supported.");
                if (c == '\\')
                {
                    if (position >= source.Length || "{}%_#&\\ ".IndexOf(source[position]) < 0) Fail("Invalid text escape.");
                    c = source[position++];
                }
                text.Append(c);
            }
            Require('}'); return text.ToString();
        }
        private string ReadCommand()
        {
            int start = position;
            while (position < source.Length && IsAsciiLetter(source[position])) position++;
            if (start == position && position < source.Length) position++;
            if (position == start) Fail("Incomplete command.");
            return source.Substring(start, position - start);
        }
        private bool AtCommand(string command)
        {
            string token = "\\" + command;
            return position + token.Length <= source.Length && string.CompareOrdinal(source, position, token, 0, token.Length) == 0
                && (!IsAsciiLetter(command[command.Length - 1]) || position + token.Length == source.Length || !IsAsciiLetter(source[position + token.Length]));
        }
        private static bool IsAsciiLetter(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
        private void SkipWhite()
        {
            while (position < source.Length)
            {
                if (char.IsWhiteSpace(source[position])) position++;
                else if (source[position] == '%') { while (position < source.Length && source[position] != '\n') position++; }
                else break;
            }
        }
        private void Require(char c) { SkipWhite(); if (position >= source.Length || source[position] != c) Fail("Expected '" + c + "'."); position++; }
        private void Fail(string message) => throw new TexParseException(message, position);
        internal static TexAtomClass Classify(string text)
        {
            if (text == "+" || text == "−" || text == "-" || text == "*" || text == "×" || text == "⋅" || text == "÷" || text == "±" || text == "∓") return TexAtomClass.Binary;
            if (new[] { "=", "<", ">", "≤", "≥", "≠", "≈", "≡", "∈", "∉", "⊂", "→", "←", "↔" }.Contains(text)) return TexAtomClass.Relation;
            if ("([{⟨⌊⌈".Contains(text) && text.Length == 1) return TexAtomClass.Open;
            if (")]}⟩⌋⌉".Contains(text) && text.Length == 1) return TexAtomClass.Close;
            if (text == "," || text == ";" || text == ":") return TexAtomClass.Punctuation;
            if (new[] { "∑", "∏", "∫", "∬", "∮" }.Contains(text)) return TexAtomClass.Operator;
            return TexAtomClass.Ordinary;
        }
    }
}
