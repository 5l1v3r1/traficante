﻿using Traficante.TSQL.Parser.Nodes;

namespace Traficante.TSQL.Evaluator.Visitors
{
    public interface IExpressionVisitor
    {
        void Visit(Node node);
        void Visit(DescNode node);
        void Visit(StarNode node);
        void Visit(FSlashNode node);
        void Visit(ModuloNode node);
        void Visit(AddNode node);
        void Visit(HyphenNode node);
        void Visit(AndNode node);
        void Visit(OrNode node);
        void Visit(EqualityNode node);
        void Visit(GreaterOrEqualNode node);
        void Visit(LessOrEqualNode node);
        void Visit(GreaterNode node);
        void Visit(LessNode node);
        void Visit(DiffNode node);
        void Visit(NotNode node);
        void Visit(LikeNode node);
        void Visit(RLikeNode node);
        void Visit(InNode node);
        void Visit(FieldNode node);
        void Visit(FieldOrderedNode node);
        void Visit(StringNode node);
        void Visit(DecimalNode node);
        void Visit(IntegerNode node);
        void Visit(BooleanNode node);
        void Visit(WordNode node);
        void Visit(ContainsNode node);
        void Visit(ExecuteNode node);
        void Visit(FunctionNode node);
        void Visit(IsNullNode node);
        void Visit(AccessColumnNode node);
        void Visit(AllColumnsNode node);
        void Visit(IdentifierNode node);
        void Visit(AccessObjectArrayNode node);
        void Visit(AccessObjectKeyNode node);
        void Visit(PropertyValueNode node);
        void Visit(VariableNode node);
        void Visit(DeclareNode node);
        void Visit(SetNode node);
        void Visit(DotNode node);
        void Visit(ArgsListNode node);
        void Visit(SelectNode node);
        void Visit(WhereNode node);
        void Visit(GroupByNode node);
        void Visit(HavingNode node);
        void Visit(SkipNode node);
        void Visit(TakeNode node);
        void Visit(TopNode node);
        void Visit(FromTableNode node);
        void Visit(FromFunctionNode node);
        void Visit(InMemoryTableFromNode node);
        void Visit(JoinNode node);
        void Visit(ExpressionFromNode node);
        void Visit(IntoNode node);
        void Visit(QueryScope node);
        void Visit(QueryNode node);
        void Visit(RootNode node);
        void Visit(SingleSetNode node);
        void Visit(UnionNode node);
        void Visit(UnionAllNode node);
        void Visit(ExceptNode node);
        void Visit(IntersectNode node);
        void Visit(PutTrueNode node);
        void Visit(MultiStatementNode node);
        void Visit(StatementsArrayNode node);
        void Visit(StatementNode node);
        void Visit(CteExpressionNode node);
        void Visit(CteInnerExpressionNode node);
        void Visit(OrderByNode node);
        void Visit(CreateTableNode node);
        void Visit(CaseNode node);
        void Visit(TypeNode node);

    }
}