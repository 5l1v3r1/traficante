﻿namespace Musoq.Parser.Tokens
{
    public class AccessPropertyToken : Token
    {
        public AccessPropertyToken(string value, TextSpan span)
            : base(value, TokenType.Property, span)
        {
            Name = value;
        }

        public string Name { get; }
    }
}