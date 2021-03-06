﻿using System.Linq;
using Traficante.TSQL.Evaluator.Visitors;
using Traficante.TSQL.Parser.Tokens;

namespace Traficante.TSQL.Parser.Nodes
{
    public class IntersectNode : SetOperatorNode
    {
        public IntersectNode(string tableName, string[] keys, Node left, Node right, bool isNested, bool isTheLastOne)
            : base(TokenType.Intersect, keys, left, right, isNested, isTheLastOne)
        {
            ResultTableName = tableName;
        }

        public override void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string ToString()
        {
            var keys = Keys.Length == 0 ? string.Empty : Keys.Aggregate((a, b) => a + "," + b);
            return $"{Left.ToString()} intersect ({keys}) {Right.ToString()}";
        }
    }
}