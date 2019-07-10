﻿namespace Traficante.TSQL.Parser.Tokens
{
    public class SelectToken : Token
    {
        public const string TokenText = "select";

        public SelectToken(TextSpan span)
            : base(TokenText, TokenType.Select, span)
        {
        }
    }
}