namespace Traficante.TSQL.Parser.Tokens
{
    public class FSlashToken : Token
    {
        public const string TokenText = "/";

        public FSlashToken(TextSpan span)
            : base(TokenText, TokenType.FSlash, span)
        {
        }
    }
}