using System.Text;

namespace StrongInject.Generator
{
    internal class AutoIndenter
    {
        private readonly StringBuilder _text = new();

        public AutoIndenter(int initialIndent)
        {
            Indent = initialIndent;
        }

        public int Indent { get; private set; }

        public void Append(string str)
        {
            _text.Append(str);
        }

        public void Append(char c)
        {
            _text.Append(c);
        }

        public void AppendIndented(string str)
        {
            _text.Append(str);
        }

        public void AppendLine(char c)
        {
            switch (c)
            {
                case '}':
                    Indent--;
                    break;
                case '{':
                    Indent++;
                    break;
            }

            _text.Append(c);
        }

        public void AppendLineIndented(string str)
        {
            _text.AppendLine(str);
        }

        public void AppendLine(string str)
        {
            switch (str[0])
            {
                case '}':
                    Indent--;
                    break;
                case '{':
                    Indent++;
                    break;
            }

            _text.AppendLine(str);
        }

        public override string ToString()
        {
            return _text.ToString();
        }

        public AutoIndenter GetSubIndenter()
        {
            return new AutoIndenter(Indent);
        }

        public void Append(AutoIndenter subIndenter)
        {
            _text.Append(subIndenter._text);
        }
    }
}